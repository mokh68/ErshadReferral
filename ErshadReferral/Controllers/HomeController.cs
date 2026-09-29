using System.Diagnostics;
using ErshadReferral.Data;
using ErshadReferral.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ErshadReferral.Controllers;

[Authorize]
public class HomeController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly UserManager<AppUser> _userManager;

    public HomeController(ApplicationDbContext db, UserManager<AppUser> userManager)
    {
        _db = db;
        _userManager = userManager;
    }

    public async Task<IActionResult> Index()
    {
        // OfficeManager gets a simplified dashboard via VisitorEntries controller
        if (User.IsInRole(AppRoles.OfficeManager) || User.IsInRole(AppRoles.BranchUser))
        {
            return RedirectToAction(nameof(VisitorEntriesController.OfficeManager), "VisitorEntries");
        }

        // EntryController gets their own dashboard
        if (User.IsInRole(AppRoles.EntryController))
        {
            return RedirectToAction(nameof(VisitorEntriesController.EntryControllerDashboard), "VisitorEntries");
        }

        var query = _db.Referrals.AsQueryable();

        if (User.IsInRole(AppRoles.BranchUser))
        {
            var user = await _userManager.GetUserAsync(User);
            if (user?.BranchId != null)
                query = query.Where(r => r.BranchId == user.BranchId);
        }
        // EntryController, IT, ErshadManager, OfficeManager see all referrals in dashboard

        var today = DateTime.Today;
        ViewBag.TotalReferrals = await query.CountAsync();
        ViewBag.TodayReferrals = await query.CountAsync(r => r.ReferralDate >= today);
        ViewBag.OpenReferrals = await query.CountAsync(r =>
            r.Status == ReferralStatus.Referred || r.Status == ReferralStatus.Received || r.Status == ReferralStatus.InProgress);
    ViewBag.ClosedReferrals = await query.CountAsync(r => r.Status == ReferralStatus.Closed);

        return View();
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    [AllowAnonymous]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}
﻿
