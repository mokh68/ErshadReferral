using ErshadReferral.Data;
using ErshadReferral.Helpers;
using ErshadReferral.Models;
using ErshadReferral.Models.ViewModels;
using ErshadReferral.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace ErshadReferral.Controllers;

[Authorize]
public class ReferralsController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly ReferralService _referralService;
    private readonly UserManager<AppUser> _userManager;

    public ReferralsController(ApplicationDbContext db, ReferralService referralService, UserManager<AppUser> userManager)
    {
        _db = db;
        _referralService = referralService;
        _userManager = userManager;
    }

    private async Task<string> GenerateUniquePlaceholderNationalCodeAsync()
    {
        // Generate a 10-digit placeholder starting with 9 to avoid colliding with real codes
        var rand = new Random();
        for (int attempt = 0; attempt < 20; attempt++)
        {
            var sb = new System.Text.StringBuilder();
            sb.Append('9');
            for (int i = 0; i < 9; i++) sb.Append(rand.Next(0, 10));
            var code = sb.ToString();
            var exists = await _db.People.AnyAsync(p => p.NationalCode == code);
            if (!exists) return code;
        }

        // Fallback deterministic: use ticks mod 1e9
        var fallback = "9" + (DateTime.UtcNow.Ticks % 1000000000).ToString("D9");
        return fallback;
    }



    public async Task<IActionResult> Index(ReferralFilterViewModel filter)
    {
        var query = _db.Referrals
            .Include(r => r.Person)
            .Include(r => r.Branch)!.ThenInclude(b => b!.Court)
            .AsQueryable();

        if (User.IsInRole(AppRoles.BranchUser))
        {
            var user = await _userManager.GetUserAsync(User);
            if (user?.BranchId == null) return Forbid();
            query = query.Where(r => r.BranchId == user.BranchId);
        }
        // EntryController, IT, ErshadManager, OfficeManager see all referrals

        if (!string.IsNullOrWhiteSpace(filter.NationalCode))
            query = query.Where(r => r.Person!.NationalCode.Contains(filter.NationalCode.Trim()));

        if (!string.IsNullOrWhiteSpace(filter.ReferralNumber))
            query = query.Where(r => r.ReferralNumber.Contains(filter.ReferralNumber.Trim()));

        if (filter.BranchId.HasValue)
            query = query.Where(r => r.BranchId == filter.BranchId.Value);

        if (filter.Status.HasValue)
            query = query.Where(r => r.Status == filter.Status.Value);

        var fromDate = PersianDateHelper.ParsePersianDate(filter.FromDate);
        if (fromDate.HasValue)
            query = query.Where(r => r.ReferralDate >= fromDate.Value);

        var toDate = PersianDateHelper.ParsePersianDate(filter.ToDate);
        if (toDate.HasValue)
            query = query.Where(r => r.ReferralDate < toDate.Value.AddDays(1));

        var referrals = await query.OrderByDescending(r => r.Id).Take(500).ToListAsync();

        await LoadBranchesAsync();
        ViewBag.Filter = filter;
        return View(referrals);
    }

    [Authorize(Roles = $"{AppRoles.IT},{AppRoles.ErshadManager},{AppRoles.OfficeManager}")]
    public async Task<IActionResult> Create()
    {
        await LoadBranchesAsync();
        var model = new ReferralCreateViewModel();
        // Prefill name and mobile from current user when available
        var user = await _userManager.GetUserAsync(User);
        if (user != null)
        {
            if (!string.IsNullOrWhiteSpace(user.FullName))
            {
                var parts = user.FullName.Trim().Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
                model.FirstName = parts.Length > 0 ? parts[0] : string.Empty;
                model.LastName = parts.Length > 1 ? parts[1] : string.Empty;
            }
            model.Mobile = user.PhoneNumber;
        }

        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> LookupPerson(string nationalCode)
    {
        if (string.IsNullOrWhiteSpace(nationalCode) || nationalCode.Trim().Length != 10)
            return Json(new { success = false, message = "کد ملی باید ۱۰ رقم باشد" });

        var trimmed = nationalCode.Trim();

        if (!NationalCodeHelper.IsValid(trimmed))
            return Json(new { success = false, message = "فرمت کد ملی نامعتبر است" });

        var person = await _db.People.FirstOrDefaultAsync(p => p.NationalCode == trimmed);
        if (person == null)
            return Json(new { success = false, notFound = true });

        return Json(new
        {
            success = true,
            nationalCode = person.NationalCode,
            firstName = person.FirstName,
            lastName = person.LastName,
            mobile = person.Mobile
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = $"{AppRoles.IT},{AppRoles.ErshadManager},{AppRoles.OfficeManager}")]
    public async Task<IActionResult> Create(ReferralCreateViewModel model)
    {
        var nationalCode = model.NationalCode?.Trim();

        if (string.IsNullOrWhiteSpace(nationalCode))
        {
            // national code not provided: generate a placeholder to satisfy schema
            nationalCode = await GenerateUniquePlaceholderNationalCodeAsync();
        }
        else
        {
            // validate provided national code
            if (!NationalCodeHelper.IsValid(nationalCode))
            {
                ModelState.AddModelError(nameof(model.NationalCode), "فرمت کد ملی نامعتبر است");
            }
        }

        if (!ModelState.IsValid)
        {
            await LoadBranchesAsync();
            return View(model);
        }

        var person = await _db.People.FirstOrDefaultAsync(p => p.NationalCode == nationalCode);

        // If person not found, use defaults for name and mobile when not provided
        if (person == null)
        {
            var first = string.IsNullOrWhiteSpace(model.FirstName) ? "مراجع" : model.FirstName!.Trim();
            var last = string.IsNullOrWhiteSpace(model.LastName) ? "کننده" : model.LastName!.Trim();
            var mobile = string.IsNullOrWhiteSpace(model.Mobile) ? "09000000000" : model.Mobile!.Trim();

            person = new Person
            {
                NationalCode = nationalCode,
                FirstName = first,
                LastName = last,
                Mobile = mobile
            };
            _db.People.Add(person);
            await _db.SaveChangesAsync();
        }

        var user = await _userManager.GetUserAsync(User);
        var roles = user != null ? await _userManager.GetRolesAsync(user) : new List<string>();
        var roleName = roles.FirstOrDefault();

        var referral = new Referral
        {
            PersonId = person.Id,
            BranchId = model.BranchId,
            Subject = model.Subject.Trim(),
            Description = model.Description?.Trim()
        };

        await _referralService.CreateAsync(referral, user?.Id, user?.FullName, roleName);

        TempData["Success"] = $"ارجاع با شماره {referral.ReferralNumber} ثبت شد";
        return RedirectToAction(nameof(Receipt), new { id = referral.Id });
    }

    public async Task<IActionResult> Details(int id)
    {
        var referral = await _db.Referrals
            .Include(r => r.Person)
            .Include(r => r.Branch)!.ThenInclude(b => b!.Court)
            .Include(r => r.StatusHistories)
            .FirstOrDefaultAsync(r => r.Id == id);

        if (referral == null) return NotFound();

        if (User.IsInRole(AppRoles.BranchUser))
        {
            var user = await _userManager.GetUserAsync(User);
            if (user?.BranchId != referral.BranchId) return Forbid();
        }
        // EntryController, IT, ErshadManager, OfficeManager can view any referral details

        return View(referral);
    }

    public async Task<IActionResult> Print(int id)
    {
        var referral = await _db.Referrals
            .Include(r => r.Person)
            .Include(r => r.Branch)!.ThenInclude(b => b!.Court)
            .FirstOrDefaultAsync(r => r.Id == id);

        if (referral == null) return NotFound();

        if (User.IsInRole(AppRoles.BranchUser))
        {
            var user = await _userManager.GetUserAsync(User);
            if (user?.BranchId != referral.BranchId) return Forbid();
        }

        return View(referral);
    }

    public async Task<IActionResult> Receipt(int id)
    {
        var referral = await _db.Referrals
            .Include(r => r.Person)
            .Include(r => r.Branch)!.ThenInclude(b => b!.Court)
            .FirstOrDefaultAsync(r => r.Id == id);

        if (referral == null) return NotFound();

        if (User.IsInRole(AppRoles.BranchUser))
        {
            var user = await _userManager.GetUserAsync(User);
            if (user?.BranchId != referral.BranchId) return Forbid();
        }

        return View(referral);
    }

    public async Task<IActionResult> ShortPrint(int id)
    {
        var referral = await _db.Referrals
            .Include(r => r.Person)
            .Include(r => r.Branch)!.ThenInclude(b => b!.Court)
            .FirstOrDefaultAsync(r => r.Id == id);

        if (referral == null) return NotFound();

        if (User.IsInRole(AppRoles.BranchUser))
        {
            var user = await _userManager.GetUserAsync(User);
            if (user?.BranchId != referral.BranchId) return Forbid();
        }

        return View(referral);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ChangeStatus(ChangeStatusViewModel model)
    {
        var referral = await _db.Referrals.FindAsync(model.ReferralId);
        if (referral == null) return NotFound();

        var user = await _userManager.GetUserAsync(User);
        if (user == null) return Forbid();
        var roles = await _userManager.GetRolesAsync(user);
        var roleName = roles.FirstOrDefault();

        var branchStatuses = new[] { ReferralStatus.Received, ReferralStatus.InProgress, ReferralStatus.Closed, ReferralStatus.ReturnedToErshad };
        var managerStatuses = new[] { ReferralStatus.Cancelled, ReferralStatus.Referred };

        bool allowed;
        if (User.IsInRole(AppRoles.BranchUser))
        {
            allowed = user.BranchId == referral.BranchId && branchStatuses.Contains(model.NewStatus);
        }
        else if (User.IsInRole(AppRoles.IT) || User.IsInRole(AppRoles.ErshadManager))
        {
            allowed = branchStatuses.Contains(model.NewStatus) || managerStatuses.Contains(model.NewStatus);
        }
        else
        {
            allowed = false;
        }

        if (!allowed) return Forbid();

        if (!ModelState.IsValid)
        {
            TempData["Error"] = "اطلاعات تغییر وضعیت نامعتبر است";
            return RedirectToAction(nameof(Details), new { id = model.ReferralId });
        }

        await _referralService.ChangeStatusAsync(model.ReferralId, model.NewStatus, model.Note,
            user.Id, user.FullName, roleName);

        TempData["Success"] = "وضعیت با موفقیت تغییر کرد";
        return RedirectToAction(nameof(Details), new { id = model.ReferralId });
    }

    private async Task LoadBranchesAsync()
    {
        var branches = await _db.Branches
            .Include(b => b.Court)
            .Where(b => b.IsActive)
            .OrderBy(b => b.Court!.Name).ThenBy(b => b.Name)
            .Select(b => new { b.Id, Display = b.Court!.Name + " - " + b.Name })
            .ToListAsync();
        ViewBag.Branches = new SelectList(branches, "Id", "Display");
    }
}
