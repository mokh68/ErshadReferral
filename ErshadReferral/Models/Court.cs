using System.ComponentModel.DataAnnotations;

namespace ErshadReferral.Models;

public enum CourtType
{
    [Display(Name = "حقوقی")] Hoghooghi = 1,
    [Display(Name = "کیفری")] Keyfari = 2,
    [Display(Name = "خانواده")] Khanevadeh = 3,
    [Display(Name = "انقلاب")] Enghelab = 4,
    [Display(Name = "شورای حل اختلاف")] Shora = 5,
    [Display(Name = "تجدیدنظر")] Tajdid = 6,
    [Display(Name = "اجرای احکام")] Ejra = 7,
    [Display(Name = "سایر")] Other = 99
}

public class Court
{
    public int Id { get; set; }

    [Required(ErrorMessage = "نام دادگاه الزامی است")]
    [MaxLength(200)]
    [Display(Name = "نام دادگاه")]
    public string Name { get; set; } = string.Empty;

    [Display(Name = "نوع دادگاه")]
    public CourtType CourtType { get; set; }

    [MaxLength(500)]
    [Display(Name = "آدرس")]
    public string? Address { get; set; }

    [MaxLength(50)]
    [Display(Name = "تلفن")]
    public string? Phone { get; set; }

    [Display(Name = "فعال")]
    public bool IsActive { get; set; } = true;

    public ICollection<Branch> Branches { get; set; } = new List<Branch>();
}
