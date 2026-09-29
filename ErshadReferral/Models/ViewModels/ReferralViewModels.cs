using System.ComponentModel.DataAnnotations;

namespace ErshadReferral.Models.ViewModels;

public class ReferralCreateViewModel
{
    [Display(Name = "مراجع")]
    public int? PersonId { get; set; }

    [Required(ErrorMessage = "کد ملی الزامی است")]
    [StringLength(10, MinimumLength = 10, ErrorMessage = "کد ملی باید ۱۰ رقم باشد")]
    [RegularExpression(@"^\d{10}$", ErrorMessage = "کد ملی فقط شامل ارقام است")]
    [Display(Name = "کد ملی")]
    public string NationalCode { get; set; } = string.Empty;

    [Display(Name = "نام")]
    public string? FirstName { get; set; }

    [Display(Name = "نام خانوادگی")]
    public string? LastName { get; set; }

    [Display(Name = "شماره موبایل")]
    public string? Mobile { get; set; }

    [Required(ErrorMessage = "انتخاب شعبه الزامی است")]
    [Display(Name = "شعبه مقصد")]
    public int BranchId { get; set; }

    [Required(ErrorMessage = "موضوع الزامی است")]
    [MaxLength(300)]
    [Display(Name = "موضوع / خلاصه خواسته")]
    public string Subject { get; set; } = string.Empty;

    [MaxLength(1000)]
    [Display(Name = "شرح")]
    public string? Description { get; set; }
}

public class ChangeStatusViewModel
{
    public int ReferralId { get; set; }

    [Required(ErrorMessage = "انتخاب وضعیت الزامی است")]
    [Display(Name = "وضعیت جدید")]
    public Models.ReferralStatus NewStatus { get; set; }

    [MaxLength(500)]
    [Display(Name = "توضیح")]
    public string? Note { get; set; }
}

public class ReferralFilterViewModel
{
    [Display(Name = "کد ملی")]
    public string? NationalCode { get; set; }

    [Display(Name = "شماره ارجاع")]
    public string? ReferralNumber { get; set; }

    [Display(Name = "شعبه")]
    public int? BranchId { get; set; }

    [Display(Name = "وضعیت")]
    public Models.ReferralStatus? Status { get; set; }

    [Display(Name = "از تاریخ")]
    public string? FromDate { get; set; }

    [Display(Name = "تا تاریخ")]
    public string? ToDate { get; set; }
}
