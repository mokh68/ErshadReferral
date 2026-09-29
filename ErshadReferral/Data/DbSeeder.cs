using ErshadReferral.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace ErshadReferral.Data;

public static class DbSeeder
{
    public static async Task SeedAsync(IServiceProvider services)
    {
        var db = services.GetRequiredService<ApplicationDbContext>();
        await db.Database.MigrateAsync();

        var roleManager = services.GetRequiredService<RoleManager<IdentityRole>>();
        var userManager = services.GetRequiredService<UserManager<AppUser>>();

        foreach (var role in AppRoles.DisplayNames.Keys)
        {
            if (!await roleManager.RoleExistsAsync(role))
            {
                await roleManager.CreateAsync(new IdentityRole(role));
            }
        }

        const string adminUserName = "admin";
        var admin = await userManager.FindByNameAsync(adminUserName);
        if (admin == null)
        {
            admin = new AppUser
            {
                UserName = adminUserName,
                FullName = "مدیر فناوری",
                IsActive = true
            };
            var result = await userManager.CreateAsync(admin, "Admin@123");
            if (result.Succeeded)
            {
                await userManager.AddToRoleAsync(admin, AppRoles.IT);
            }
        }

        // ==================== Seed Courts & Branches ====================
        // اجرای احکام شیراز
        var ejraShirazCourt = await db.Courts.FirstOrDefaultAsync(c => c.Name == "اجرای احکام شیراز");
        if (ejraShirazCourt == null)
        {
            ejraShirazCourt = new Court
            {
                Name = "اجرای احکام شیراز",
                CourtType = CourtType.Ejra,
                IsActive = true
            };
            db.Courts.Add(ejraShirazCourt);
            await db.SaveChangesAsync();

            // شعب ۱ تا ۱۸ اجرای احکام حقوقی
            for (int i = 1; i <= 18; i++)
            {
                db.Branches.Add(new Branch
                {
                    Name = $"شعبه {i} اجرای احکام حقوقی",
                    CourtId = ejraShirazCourt.Id,
                    IsActive = true
                });
            }
            await db.SaveChangesAsync();
        }

        // Seed OfficeManager user for Branch 1
        const string officeManagerUserName = "dabir1";
        var officeManager = await userManager.FindByNameAsync(officeManagerUserName);
        if (officeManager == null)
        {
            officeManager = new AppUser
            {
                UserName = officeManagerUserName,
                FullName = "مدیر دفتر شعبه 101",
                BranchId = 1,
                IsActive = true
            };
            var result = await userManager.CreateAsync(officeManager, "Admin@123");
            if (result.Succeeded)
            {
                await userManager.AddToRoleAsync(officeManager, AppRoles.OfficeManager);
            }
        }

        // Seed EntryController user
        const string entryControllerUserName = "voroodi";
        var entryController = await userManager.FindByNameAsync(entryControllerUserName);
        if (entryController == null)
        {
            entryController = new AppUser
            {
                UserName = entryControllerUserName,
                FullName = "کنترل کننده ورودی",
                IsActive = true
            };
            var result = await userManager.CreateAsync(entryController, "Admin@123");
            if (result.Succeeded)
            {
                await userManager.AddToRoleAsync(entryController, AppRoles.EntryController);
            }
        }
    }
}
