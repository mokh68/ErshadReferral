using ErshadReferral.Data;
using ErshadReferral.Helpers;
using ErshadReferral.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ErshadReferral.Controllers;

[Authorize(Roles = $"{AppRoles.IT},{AppRoles.ErshadManager}")]
public class ReportsController : Controller
{
    private readonly ApplicationDbContext _db;

    public ReportsController(ApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<IActionResult> Index(string? fromDate, string? toDate)
    {
        var from = PersianDateHelper.ParsePersianDate(fromDate) ?? DateTime.Now.AddMonths(-1);
        var to = (PersianDateHelper.ParsePersianDate(toDate) ?? DateTime.Now).AddDays(1);

        var query = _db.Referrals.Where(r => r.ReferralDate >= from && r.ReferralDate < to);

        var byBranch = await query
            .GroupBy(r => new { r.Branch!.Name, CourtName = r.Branch.Court!.Name })
            .Select(g => new BranchReportRow
            {
                BranchName = g.Key.CourtName + " - " + g.Key.Name,
                Total = g.Count(),
                Open = g.Count(r => r.Status == ReferralStatus.Referred || r.Status == ReferralStatus.Received || r.Status == ReferralStatus.InProgress),
                Closed = g.Count(r => r.Status == ReferralStatus.Closed),
                Returned = g.Count(r => r.Status == ReferralStatus.ReturnedToErshad),
                Cancelled = g.Count(r => r.Status == ReferralStatus.Cancelled)
            })
            .OrderByDescending(x => x.Total)
            .ToListAsync();

        var byStatus = await query
            .GroupBy(r => r.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToListAsync();

        // Average waiting/visit time per branch for visitor entries
        // Use ArrivalTime if available, otherwise ExitTime, to calculate duration
        var allVisitors = await _db.VisitorEntries
            .Include(v => v.Branch)!.ThenInclude(b => b!.Court)
            .Where(v => v.EntryTime >= from && v.EntryTime < to)
            .ToListAsync();

        var waitingTimeByBranch = allVisitors
            .Where(v => v.ArrivalTime.HasValue || v.ExitTime.HasValue)
            .GroupBy(v => v.Branch!.Court!.Name + " - " + v.Branch!.Name)
            .Select(g =>
            {
                var durations = g.Select(v =>
                {
                    var endTime = v.ArrivalTime ?? v.ExitTime!.Value;
                    return (endTime - v.EntryTime).TotalMinutes;
                }).ToList();
                return new WaitingTimeReportRow
                {
                    BranchName = g.Key,
                    TotalCount = g.Count(),
                    AvgWaitMinutes = durations.Average(),
                    MinWaitMinutes = (int)durations.Min(),
                    MaxWaitMinutes = (int)durations.Max()
                };
            })
            .OrderByDescending(x => x.AvgWaitMinutes)
            .ToList();

        // Overall average
        var allDurations = allVisitors
            .Where(v => v.ArrivalTime.HasValue || v.ExitTime.HasValue)
            .Select(v => ((v.ArrivalTime ?? v.ExitTime!.Value) - v.EntryTime).TotalMinutes)
            .ToList();
        ViewBag.AvgWaitMinutes = allDurations.Any() ? allDurations.Average() : 0;
        ViewBag.TotalVisitorsWithArrival = allDurations.Count;

        ViewBag.ByStatus = byStatus.ToDictionary(x => x.Status, x => x.Count);
        ViewBag.FromDate = from.ToPersianDate();
        ViewBag.ToDate = to.AddDays(-1).ToPersianDate();
        ViewBag.Total = byBranch.Sum(x => x.Total);
        ViewBag.WaitingTimeByBranch = waitingTimeByBranch;

        return View(byBranch);
    }
}

public class BranchReportRow
{
    public string BranchName { get; set; } = string.Empty;
    public int Total { get; set; }
    public int Open { get; set; }
    public int Closed { get; set; }
    public int Returned { get; set; }
    public int Cancelled { get; set; }
}

public class WaitingTimeReportRow
{
    public string BranchName { get; set; } = string.Empty;
    public int TotalCount { get; set; }
    public double AvgWaitMinutes { get; set; }
    public int MinWaitMinutes { get; set; }
    public int MaxWaitMinutes { get; set; }
}
