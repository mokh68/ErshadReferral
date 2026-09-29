using ErshadReferral.Data;
using ErshadReferral.Helpers;
using ErshadReferral.Models;
using Microsoft.EntityFrameworkCore;

namespace ErshadReferral.Services;

public class ReferralService
{
    private readonly ApplicationDbContext _db;

    public ReferralService(ApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<Referral> CreateAsync(Referral referral, string? userId, string? userName, string? roleName)
    {
        referral.ReferralNumber = await GenerateReferralNumberAsync();
        referral.Status = ReferralStatus.Referred;
        referral.ReferralDate = DateTime.Now;
        referral.CreatedByUserId = userId;
        referral.CreatedByUserName = userName;

        referral.StatusHistories.Add(new ReferralStatusHistory
        {
            Status = ReferralStatus.Referred,
            Note = "ثبت ارجاع اولیه از واحد ارشاد",
            ChangedAt = DateTime.Now,
            ChangedByUserId = userId,
            ChangedByUserName = userName,
            ChangedByRole = roleName
        });

        _db.Referrals.Add(referral);
        await _db.SaveChangesAsync();
        return referral;
    }

    public async Task<bool> ChangeStatusAsync(int referralId, ReferralStatus newStatus, string? note,
        string? userId, string? userName, string? roleName)
    {
        var referral = await _db.Referrals.FindAsync(referralId);
        if (referral == null) return false;

        referral.Status = newStatus;
        _db.ReferralStatusHistories.Add(new ReferralStatusHistory
        {
            ReferralId = referralId,
            Status = newStatus,
            Note = note,
            ChangedAt = DateTime.Now,
            ChangedByUserId = userId,
            ChangedByUserName = userName,
            ChangedByRole = roleName
        });

        await _db.SaveChangesAsync();
        return true;
    }

    private async Task<string> GenerateReferralNumberAsync()
    {
        var persianYear = PersianDateHelper.GetPersianYear(DateTime.Now);
        var prefix = $"{persianYear}-";

        var lastNumber = await _db.Referrals
            .Where(r => r.ReferralNumber.StartsWith(prefix))
            .OrderByDescending(r => r.ReferralNumber)
            .Select(r => r.ReferralNumber)
            .FirstOrDefaultAsync();

        var counter = 1;
        if (lastNumber != null && int.TryParse(lastNumber.Substring(prefix.Length), out var lastCounter))
        {
            counter = lastCounter + 1;
        }

        return $"{prefix}{counter:00000}";
    }
}
