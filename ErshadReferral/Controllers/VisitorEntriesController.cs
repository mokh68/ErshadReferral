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
public class VisitorEntriesController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly UserManager<AppUser> _userManager;
    private readonly ReferralService _referralService;

    public VisitorEntriesController(ApplicationDbContext db, UserManager<AppUser> userManager, ReferralService referralService)
    {
        _db = db;
        _userManager = userManager;
        _referralService = referralService;
    }

    public async Task<IActionResult> Index(VisitorFilterViewModel filter)
    {
        var query = _db.VisitorEntries
            .Include(v => v.Branch)!.ThenInclude(b => b!.Court)
            .AsQueryable();

        // BranchUser and OfficeManager can only see their branch's visitors
        if (User.IsInRole(AppRoles.BranchUser) || User.IsInRole(AppRoles.OfficeManager))
        {
            var user = await _userManager.GetUserAsync(User);
            if (user?.BranchId == null) return Forbid();
            query = query.Where(v => v.BranchId == user.BranchId);
        }
        // EntryController, IT, ErshadManager see all entries

        if (!string.IsNullOrWhiteSpace(filter.VisitorName))
            query = query.Where(v => v.VisitorName.Contains(filter.VisitorName.Trim()));

        if (!string.IsNullOrWhiteSpace(filter.NationalCode))
            query = query.Where(v => v.NationalCode!.Contains(filter.NationalCode.Trim()));

        if (filter.BranchId.HasValue)
            query = query.Where(v => v.BranchId == filter.BranchId.Value);

        if (filter.OnlyActive == true)
            query = query.Where(v => !v.ExitTime.HasValue);

        var fromDate = PersianDateHelper.ParsePersianDate(filter.FromDate);
        if (fromDate.HasValue)
            query = query.Where(v => v.EntryTime >= fromDate.Value);

        var toDate = PersianDateHelper.ParsePersianDate(filter.ToDate);
        if (toDate.HasValue)
            query = query.Where(v => v.EntryTime < toDate.Value.AddDays(1));

        var entries = await query.OrderByDescending(v => v.EntryTime).Take(500).ToListAsync();

        await LoadBranchesAsync();
        ViewBag.Filter = filter;
        ViewBag.TodayCount = await _db.VisitorEntries
            .CountAsync(v => v.EntryTime.Date == DateTime.Today);
        ViewBag.CurrentlyInside = await _db.VisitorEntries
            .CountAsync(v => !v.ExitTime.HasValue);

        return View(entries);
    }

    // Only Ershad unit (IT, ErshadManager) can register new visitor entries
    [Authorize(Roles = $"{AppRoles.IT},{AppRoles.ErshadManager}")]
    public async Task<IActionResult> Create()
    {
        var model = new VisitorEntryCreateViewModel();
        await LoadBranchesAsync();
        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = $"{AppRoles.IT},{AppRoles.ErshadManager}")]
    public async Task<IActionResult> Create(VisitorEntryCreateViewModel model)
    {
        var user = await _userManager.GetUserAsync(User);

        if (!string.IsNullOrWhiteSpace(model.NationalCode) && !NationalCodeHelper.IsValid(model.NationalCode.Trim()))
        {
            ModelState.AddModelError(nameof(model.NationalCode), "فرمت کد ملی نامعتبر است");
        }

        if (!ModelState.IsValid)
        {
            await LoadBranchesAsync();
            return View(model);
        }

        var entry = new VisitorEntry
        {
            BranchId = model.BranchId,
            VisitorName = model.VisitorName.Trim(),
            NationalCode = model.NationalCode?.Trim(),
            Mobile = model.Mobile?.Trim(),
            Subject = model.Subject.Trim(),
            Notes = model.Notes?.Trim(),
            EntryTime = DateTime.Now,
            RegisteredByUserId = user?.Id,
            RegisteredByUserName = user?.FullName
        };

        _db.VisitorEntries.Add(entry);
        await _db.SaveChangesAsync();

        TempData["Success"] = $"ورود مراجعه‌کننده «{entry.VisitorName}» ثبت شد";
        return RedirectToAction(nameof(Index));
    }

    // OfficeManager confirms visitor arrival at the branch
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RegisterArrival(int id)
    {
        var entry = await _db.VisitorEntries.FindAsync(id);
        if (entry == null) return NotFound();

        // OfficeManager/BranchUser can confirm arrival only for their own branch
        if (User.IsInRole(AppRoles.OfficeManager) || User.IsInRole(AppRoles.BranchUser))
        {
            var user = await _userManager.GetUserAsync(User);
            if (user?.BranchId != entry.BranchId) return Forbid();
        }
        // IT, ErshadManager, EntryController can confirm arrival for any branch
        else if (!User.IsInRole(AppRoles.IT) && !User.IsInRole(AppRoles.ErshadManager) && !User.IsInRole(AppRoles.EntryController))
        {
            return Forbid();
        }

        if (entry.ArrivalTime.HasValue)
        {
            TempData["Error"] = "حضور قبلاً ثبت شده است";
            return RedirectToAction(nameof(OfficeManager));
        }

        entry.ArrivalTime = DateTime.Now;
        await _db.SaveChangesAsync();

        TempData["Success"] = $"حضور «{entry.VisitorName}» ثبت شد";
        return RedirectToAction(nameof(OfficeManager));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RegisterExit(int id)
    {
        var entry = await _db.VisitorEntries.FindAsync(id);
        if (entry == null) return NotFound();

        // BranchUser/OfficeManager can only manage their branch
        if (User.IsInRole(AppRoles.BranchUser) || User.IsInRole(AppRoles.OfficeManager))
        {
            var user = await _userManager.GetUserAsync(User);
            if (user?.BranchId != entry.BranchId) return Forbid();
        }

        if (entry.ExitTime.HasValue)
        {
            TempData["Error"] = "خروج قبلاً ثبت شده است";
            return RedirectToAction(nameof(Index));
        }

        entry.ExitTime = DateTime.Now;
        await _db.SaveChangesAsync();

        TempData["Success"] = $"خروج «{entry.VisitorName}» ثبت شد";
        return RedirectToAction(nameof(OfficeManager));
    }

    // OfficeManager dashboard - managing attendance for visitors AND referrals
    public async Task<IActionResult> OfficeManager()
    {
        var user = await _userManager.GetUserAsync(User);
        if (user?.BranchId == null) return Forbid();

        var branchId = user.BranchId.Value;
        var today = DateTime.Today;

        // Today's visitors for this branch
        var todayVisitors = await _db.VisitorEntries
            .Include(v => v.Branch)
            .Where(v => v.BranchId == branchId && v.EntryTime.Date == today)
            .OrderByDescending(v => v.EntryTime)
            .ToListAsync();

        // Referrals for this branch
        var waitingReferrals = await _db.Referrals
            .Include(r => r.Person)
            .Include(r => r.Branch)
            .Where(r => r.BranchId == branchId && r.IsVisibleToBranches && r.Status == ReferralStatus.Referred)
            .OrderByDescending(r => r.ReferralDate)
            .ToListAsync();

        var activeReferrals = await _db.Referrals
            .Include(r => r.Person)
            .Include(r => r.Branch)
            .Where(r => r.BranchId == branchId && r.IsVisibleToBranches && (r.Status == ReferralStatus.Received || r.Status == ReferralStatus.InProgress))
            .OrderByDescending(r => r.ReferralDate)
            .ToListAsync();

        var closedReferrals = await _db.Referrals
            .Include(r => r.Person)
            .Include(r => r.Branch)
            .Where(r => r.BranchId == branchId && r.IsVisibleToBranches && (r.Status == ReferralStatus.Closed || r.Status == ReferralStatus.ReturnedToErshad))
            .OrderByDescending(r => r.ReferralDate)
            .Take(20)
            .ToListAsync();

        var branchName = await _db.Branches
            .Where(b => b.Id == branchId)
            .Select(b => b.Name)
            .FirstOrDefaultAsync();

        var vm = new OfficeManagerDashboardViewModel
        {
            BranchName = branchName ?? "",
            TodayVisitorCount = todayVisitors.Count,
            WaitingVisitorsCount = todayVisitors.Count(v => !v.ArrivalTime.HasValue && !v.ExitTime.HasValue),
            ArrivedVisitorsCount = todayVisitors.Count(v => v.ArrivalTime.HasValue && !v.ExitTime.HasValue),
            ExitedVisitorsCount = todayVisitors.Count(v => v.ExitTime.HasValue),
            WaitingReferralsCount = waitingReferrals.Count,
            ActiveReferralsCount = activeReferrals.Count,
            ClosedReferralsCount = closedReferrals.Count,
            WaitingVisitors = todayVisitors.Where(v => !v.ArrivalTime.HasValue && !v.ExitTime.HasValue).ToList(),
            ArrivedVisitors = todayVisitors.Where(v => v.ArrivalTime.HasValue && !v.ExitTime.HasValue).ToList(),
            ExitedVisitors = todayVisitors.Where(v => v.ExitTime.HasValue).ToList(),
            WaitingReferrals = waitingReferrals,
            ActiveReferrals = activeReferrals,
            ClosedReferrals = closedReferrals
        };

        return View("~/Views/Home/OfficeManagerDashboard.cshtml", vm);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RegisterReferralArrival(int id)
    {
        var referral = await _db.Referrals.FindAsync(id);
        if (referral == null) return NotFound();

        if ((User.IsInRole(AppRoles.OfficeManager) || User.IsInRole(AppRoles.BranchUser)) && !referral.IsVisibleToBranches)
            return NotFound();

        if (User.IsInRole(AppRoles.OfficeManager) || User.IsInRole(AppRoles.BranchUser))
        {
            var user = await _userManager.GetUserAsync(User);
            if (user?.BranchId != referral.BranchId) return Forbid();
        }
        else if (!User.IsInRole(AppRoles.IT) && !User.IsInRole(AppRoles.ErshadManager) && !User.IsInRole(AppRoles.EntryController))
        {
            return Forbid();
        }

        if (referral.Status != ReferralStatus.Referred)
        {
            TempData["Error"] = "این ارجاع قبلاً دریافت شده است";
            return RedirectToAction(nameof(OfficeManager));
        }

        var currentUser = await _userManager.GetUserAsync(User);
        var roles = currentUser != null ? await _userManager.GetRolesAsync(currentUser) : new List<string>();
        await _referralService.ChangeStatusAsync(id, ReferralStatus.Received, "تأیید حضور توسط مدیر/کاربر شعبه",
            currentUser?.Id, currentUser?.FullName, roles.FirstOrDefault());

        TempData["Success"] = $"حضور «{referral.Person?.FullName}» تأیید شد";
        return RedirectToAction(nameof(OfficeManager));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RegisterReferralExit(int id)
    {
        var referral = await _db.Referrals.FindAsync(id);
        if (referral == null) return NotFound();

        if ((User.IsInRole(AppRoles.OfficeManager) || User.IsInRole(AppRoles.BranchUser)) && !referral.IsVisibleToBranches)
            return NotFound();

        if (User.IsInRole(AppRoles.OfficeManager) || User.IsInRole(AppRoles.BranchUser))
        {
            var user = await _userManager.GetUserAsync(User);
            if (user?.BranchId != referral.BranchId) return Forbid();
        }
        else if (!User.IsInRole(AppRoles.IT) && !User.IsInRole(AppRoles.ErshadManager) && !User.IsInRole(AppRoles.EntryController))
        {
            return Forbid();
        }

        if (referral.Status != ReferralStatus.Received && referral.Status != ReferralStatus.InProgress)
        {
            TempData["Error"] = "وضعیت ارجاع برای ثبت خروج مناسب نیست";
            return RedirectToAction(nameof(OfficeManager));
        }

        var currentUser = await _userManager.GetUserAsync(User);
        var roles = currentUser != null ? await _userManager.GetRolesAsync(currentUser) : new List<string>();
        await _referralService.ChangeStatusAsync(id, ReferralStatus.Closed, "بسته شدن توسط مدیر/کاربر شعبه",
            currentUser?.Id, currentUser?.FullName, roles.FirstOrDefault());

        TempData["Success"] = $"ارجاع «{referral.Person?.FullName}» مختومه شد";
        return RedirectToAction(nameof(OfficeManager));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        // Only IT can delete
        if (!User.IsInRole(AppRoles.IT)) return Forbid();

        var entry = await _db.VisitorEntries.FindAsync(id);
        if (entry == null) return NotFound();

        _db.VisitorEntries.Remove(entry);
        await _db.SaveChangesAsync();

        TempData["Success"] = "رکورد حذف شد";
        return RedirectToAction(nameof(Index));
    }

    // ==================== Entry Controller Dashboard ====================
    // Dashboard for EntryController: shows today's pending referrals + barcode lookup
    [Authorize(Roles = $"{AppRoles.IT},{AppRoles.ErshadManager},{AppRoles.EntryController}")]
    public async Task<IActionResult> EntryControllerDashboard()
    {
        var today = DateTime.Today;

        // Today's referrals with status Referred (not yet arrived)
        var pendingReferrals = await _db.Referrals
            .Include(r => r.Person)
            .Include(r => r.Branch)!.ThenInclude(b => b!.Court)
            .Where(r => r.Status == ReferralStatus.Referred && r.ReferralDate.Date == today)
            .OrderByDescending(r => r.ReferralDate)
            .ToListAsync();

        // Today's arrived referrals
        var arrivedReferrals = await _db.Referrals
            .Include(r => r.Person)
            .Include(r => r.Branch)!.ThenInclude(b => b!.Court)
            .Where(r => (r.Status == ReferralStatus.Received || r.Status == ReferralStatus.InProgress) && r.ReferralDate.Date == today)
            .OrderByDescending(r => r.ReferralDate)
            .ToListAsync();

        ViewBag.PendingCount = pendingReferrals.Count;
        ViewBag.ArrivedCount = arrivedReferrals.Count;
        ViewBag.TotalToday = pendingReferrals.Count + arrivedReferrals.Count;

        return View(pendingReferrals);
    }

    // AJAX: Look up person by national code and auto-register entry
    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = $"{AppRoles.IT},{AppRoles.ErshadManager},{AppRoles.EntryController}")]
    public async Task<IActionResult> LookupByNationalCode(string nationalCode)
    {
        if (string.IsNullOrWhiteSpace(nationalCode) || nationalCode.Length != 10)
            return Json(new { success = false, message = "کد ملی باید ۱۰ رقم باشد" });

        var trimmedCode = nationalCode.Trim();

        if (!NationalCodeHelper.IsValid(trimmedCode))
            return Json(new { success = false, message = "فرمت کد ملی نامعتبر است" });

        var today = DateTime.Today;

        // Find person by national code
        var person = await _db.People.FirstOrDefaultAsync(p => p.NationalCode == trimmedCode);
        if (person == null)
            return Json(new { success = false, message = $"فردی با کد ملی {trimmedCode} یافت نشد" });

        // Find pending referrals for this person today
        var pendingReferrals = await _db.Referrals
            .Include(r => r.Branch)!.ThenInclude(b => b!.Court)
            .Where(r => r.PersonId == person.Id && r.Status == ReferralStatus.Referred && r.ReferralDate.Date == today)
            .ToListAsync();

        if (!pendingReferrals.Any())
        {
            // Check if already arrived today
            var alreadyArrived = await _db.Referrals
                .Include(r => r.Branch)
                .Where(r => r.PersonId == person.Id && r.ReferralDate.Date == today &&
                    (r.Status == ReferralStatus.Received || r.Status == ReferralStatus.InProgress))
                .ToListAsync();

            if (alreadyArrived.Any())
                return Json(new { success = false, message = $"حضور {person.FullName} امروز قبلاً ثبت شده است" });

            return Json(new { success = false, message = $"هیچ ارجاع منتظر ورودی برای {person.FullName} یافت نشد" });
        }

        var user = await _userManager.GetUserAsync(User);
        var roles = user != null ? await _userManager.GetRolesAsync(user) : new List<string>();
        var roleName = roles.FirstOrDefault();

        var registeredNames = new List<string>();

        foreach (var referral in pendingReferrals)
        {
            // Create VisitorEntry for this referral's branch
            var visitorEntry = new VisitorEntry
            {
                BranchId = referral.BranchId,
                VisitorName = person.FullName,
                NationalCode = person.NationalCode,
                Mobile = person.Mobile,
                Subject = referral.Subject,
                Notes = $"ارجاع شماره {referral.ReferralNumber}",
                EntryTime = DateTime.Now,
                ArrivalTime = DateTime.Now,
                RegisteredByUserId = user?.Id,
                RegisteredByUserName = user?.FullName
            };
            _db.VisitorEntries.Add(visitorEntry);

            // Change referral status to Received
            await _referralService.ChangeStatusAsync(referral.Id, ReferralStatus.Received,
                "ورود خودکار توسط کنترل‌کننده ورودی (اسکن کد ملی)",
                user?.Id, user?.FullName, roleName);

            registeredNames.Add($"{referral.Branch?.Court?.Name} - {referral.Branch?.Name}");
        }

        await _db.SaveChangesAsync();

        return Json(new
        {
            success = true,
            message = $"حضور {person.FullName} ثبت شد",
            branches = string.Join("، ", registeredNames),
            name = person.FullName
        });
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
