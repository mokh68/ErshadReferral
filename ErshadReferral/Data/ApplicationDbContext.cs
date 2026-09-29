using ErshadReferral.Models;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace ErshadReferral.Data;

public class ApplicationDbContext : IdentityDbContext<AppUser>
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options)
    {
    }

    public DbSet<Court> Courts => Set<Court>();
    public DbSet<Branch> Branches => Set<Branch>();
    public DbSet<Person> People => Set<Person>();
    public DbSet<Referral> Referrals => Set<Referral>();
    public DbSet<ReferralStatusHistory> ReferralStatusHistories => Set<ReferralStatusHistory>();
    public DbSet<VisitorEntry> VisitorEntries => Set<VisitorEntry>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<Person>()
            .HasIndex(p => p.NationalCode)
            .IsUnique();

        builder.Entity<Referral>()
            .HasIndex(r => r.ReferralNumber)
            .IsUnique();

        builder.Entity<Referral>()
            .HasIndex(r => r.ReferralDate);

        builder.Entity<Branch>()
            .HasOne(b => b.Court)
            .WithMany(c => c.Branches)
            .HasForeignKey(b => b.CourtId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<Referral>()
            .HasOne(r => r.Person)
            .WithMany(p => p.Referrals)
            .HasForeignKey(r => r.PersonId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<Referral>()
            .HasOne(r => r.Branch)
            .WithMany(b => b.Referrals)
            .HasForeignKey(r => r.BranchId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<ReferralStatusHistory>()
            .HasOne(h => h.Referral)
            .WithMany(r => r.StatusHistories)
            .HasForeignKey(h => h.ReferralId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Entity<AppUser>()
            .HasOne(u => u.Branch)
            .WithMany()
            .HasForeignKey(u => u.BranchId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
