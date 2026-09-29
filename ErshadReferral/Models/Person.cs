using System.ComponentModel.DataAnnotations;

namespace ErshadReferral.Models;

public class Person
{
    public int Id { get; set; }

    [Required(ErrorMessage = "نام الزامی است")]
    [MaxLength(100)]
    [Display(Name = "نام")]
    public string FirstName { get; set; } = string.Empty;

    [Required(ErrorMessage = "نام خانوادگی الزامی است")]
    [MaxLength(100)]
    [Display(Name = "نام خانوادگی")]
    public string LastName { get; set; } = string.Empty;

    [Required(ErrorMessage = "کد ملی الزامی است")]
    [StringLength(10, MinimumLength = 10, ErrorMessage = "کد ملی باید ۱۰ رقم باشد")]
    [RegularExpression(@"^\d{10}$", ErrorMessage = "کد ملی فقط شامل ارقام است")]
    [Display(Name = "کد ملی")]
    public string NationalCode { get; set; } = string.Empty;

    [MaxLength(20)]
    [Display(Name = "شماره موبایل")]
    public string? Mobile { get; set; }

    [MaxLength(500)]
    [Display(Name = "توضیحات")]
    public string? Notes { get; set; }

    [Display(Name = "تاریخ ثبت")]
    public DateTime CreatedAt { get; set; } = DateTime.Now;

    [Display(Name = "نام کامل")]
    public string FullName => $"{FirstName} {LastName}";

    public ICollection<Referral> Referrals { get; set; } = new List<Referral>();
}
