using Microsoft.AspNetCore.Identity;
using System.ComponentModel.DataAnnotations;

namespace ErshadReferral.Models;

public static class AppRoles
{
    public const string IT = "IT";
    public const string ErshadManager = "ErshadManager";
    public const string OfficeManager = "OfficeManager";
    public const string BranchUser = "BranchUser";
    public const string EntryController = "EntryController";

    public static readonly Dictionary<string, string> DisplayNames = new()
    {
        [IT] = "فناوری",
        [ErshadManager] = "مدیر ارشاد",
        [OfficeManager] = "مدیر دفتر",
        [BranchUser] = "کاربر شعبه",
        [EntryController] = "کنترل کننده ورودی"
    };
}

public class AppUser : IdentityUser
{
    [MaxLength(150)]
    [Display(Name = "نام و نام خانوادگی")]
    public string FullName { get; set; } = string.Empty;

    [Display(Name = "شعبه")]
    public int? BranchId { get; set; }
    public Branch? Branch { get; set; }

    [Display(Name = "فعال")]
    public bool IsActive { get; set; } = true;
}
