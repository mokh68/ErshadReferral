using ErshadReferral.Data;
using ErshadReferral.Models;
using ErshadReferral.Models.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace ErshadReferral.Controllers;

[Authorize(Roles = AppRoles.IT)]
public class UsersController : Controller
{
    private readonly UserManager<AppUser> _userManager;
    private readonly ApplicationDbContext _db;

    public UsersController(UserManager<AppUser> userManager, ApplicationDbContext db)
    {
        _userManager = userManager;
        _db = db;
    }

    public async Task<IActionResult> Index()
    {
        var users = await _userManager.Users.Include(u => u.Branch).OrderBy(u => u.UserName).ToListAsync();
        var userRoles = new Dictionary<string, string>();
        foreach (var user in users)
        {
            var roles = await _userManager.GetRolesAsync(user);
            var role = roles.FirstOrDefault() ?? "";
            userRoles[user.Id] = AppRoles.DisplayNames.TryGetValue(role, out var name) ? name : role;
        }
        ViewBag.UserRoles = userRoles;
        return View(users);
    }

    public async Task<IActionResult> Create()
    {
        await LoadFormDataAsync();
        return View(new UserFormViewModel());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(UserFormViewModel model)
    {
        if (string.IsNullOrWhiteSpace(model.Password))
            ModelState.AddModelError(nameof(model.Password), "رمز عبور الزامی است");

        if ((model.Role == AppRoles.BranchUser || model.Role == AppRoles.OfficeManager) && model.BranchId == null)
            ModelState.AddModelError(nameof(model.BranchId), "برای نقش‌های کاربر شعبه و مدیر دفتر، انتخاب شعبه الزامی است");

        if (!ModelState.IsValid)
        {
            await LoadFormDataAsync();
            return View(model);
        }

        var user = new AppUser
        {
            UserName = model.UserName.Trim(),
            FullName = model.FullName.Trim(),
            BranchId = (model.Role == AppRoles.BranchUser || model.Role == AppRoles.OfficeManager) ? model.BranchId : null,
            IsActive = model.IsActive
        };

        var result = await _userManager.CreateAsync(user, model.Password!);
        if (!result.Succeeded)
        {
            foreach (var error in result.Errors)
                ModelState.AddModelError(string.Empty, error.Description);
            await LoadFormDataAsync();
            return View(model);
        }

        await _userManager.AddToRoleAsync(user, model.Role);
        TempData["Success"] = "کاربر با موفقیت ایجاد شد";
        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> Edit(string id)
    {
        var user = await _userManager.FindByIdAsync(id);
        if (user == null) return NotFound();

        var roles = await _userManager.GetRolesAsync(user);
        await LoadFormDataAsync();
        return View(new UserFormViewModel
        {
            Id = user.Id,
            UserName = user.UserName!,
            FullName = user.FullName,
            Role = roles.FirstOrDefault() ?? "",
            BranchId = user.BranchId,
            IsActive = user.IsActive
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(UserFormViewModel model)
    {
        if (model.Id == null) return NotFound();
        var user = await _userManager.FindByIdAsync(model.Id);
        if (user == null) return NotFound();

        if ((model.Role == AppRoles.BranchUser || model.Role == AppRoles.OfficeManager) && model.BranchId == null)
            ModelState.AddModelError(nameof(model.BranchId), "برای نقش‌های کاربر شعبه و مدیر دفتر، انتخاب شعبه الزامی است");

        if (!ModelState.IsValid)
        {
            await LoadFormDataAsync();
            return View(model);
        }

        user.UserName = model.UserName.Trim();
        user.FullName = model.FullName.Trim();
        user.BranchId = (model.Role == AppRoles.BranchUser || model.Role == AppRoles.OfficeManager) ? model.BranchId : null;
        user.IsActive = model.IsActive;

        var result = await _userManager.UpdateAsync(user);
        if (!result.Succeeded)
        {
            foreach (var error in result.Errors)
                ModelState.AddModelError(string.Empty, error.Description);
            await LoadFormDataAsync();
            return View(model);
        }

        var currentRoles = await _userManager.GetRolesAsync(user);
        await _userManager.RemoveFromRolesAsync(user, currentRoles);
        await _userManager.AddToRoleAsync(user, model.Role);

        if (!string.IsNullOrWhiteSpace(model.Password))
        {
            var token = await _userManager.GeneratePasswordResetTokenAsync(user);
            var passResult = await _userManager.ResetPasswordAsync(user, token, model.Password);
            if (!passResult.Succeeded)
            {
                foreach (var error in passResult.Errors)
                    ModelState.AddModelError(string.Empty, error.Description);
                await LoadFormDataAsync();
                return View(model);
            }
        }

        TempData["Success"] = "کاربر با موفقیت ویرایش شد";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleActive(string id)
    {
        var user = await _userManager.FindByIdAsync(id);
        if (user == null) return NotFound();

        user.IsActive = !user.IsActive;
        await _userManager.UpdateAsync(user);

        var status = user.IsActive ? "فعال" : "غیرفعال";
        TempData["Success"] = "کاربر " + user.FullName + " " + status + " شد";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(string id)
    {
        var user = await _userManager.FindByIdAsync(id);
        if (user == null) return NotFound();

        // Prevent deleting the main admin
        var roles = await _userManager.GetRolesAsync(user);
        if (roles.Contains(AppRoles.IT))
        {
            TempData["Error"] = "امکان حذف کاربر مدیر فناوری وجود ندارد";
            return RedirectToAction(nameof(Index));
        }

        await _userManager.DeleteAsync(user);
        TempData["Success"] = "کاربر " + user.FullName + " حذف شد";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ResetPassword(string id)
    {
        var user = await _userManager.FindByIdAsync(id);
        if (user == null) return NotFound();

        var token = await _userManager.GeneratePasswordResetTokenAsync(user);
        var result = await _userManager.ResetPasswordAsync(user, token, "Admin@123");

        if (result.Succeeded)
            TempData["Success"] = "رمز عبور کاربر " + user.FullName + " به Admin@123 بازنشانی شد";
        else
            TempData["Error"] = "خطا در بازنشانی رمز عبور";

        return RedirectToAction(nameof(Index));
    }

    private async Task LoadFormDataAsync()
    {
        ViewBag.Roles = AppRoles.DisplayNames
            .Select(kv => new SelectListItem(kv.Value, kv.Key))
            .ToList();

        var branches = await _db.Branches
            .Include(b => b.Court)
            .Where(b => b.IsActive)
            .OrderBy(b => b.Court!.Name).ThenBy(b => b.Name)
            .Select(b => new { b.Id, Display = b.Court!.Name + " - " + b.Name })
            .ToListAsync();
        ViewBag.Branches = new SelectList(branches, "Id", "Display");
    }
}
