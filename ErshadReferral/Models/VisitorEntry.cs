using System.ComponentModel.DataAnnotations;

namespace ErshadReferral.Models;

public class VisitorEntry
{
    public int Id { get; set; }

    [Display(Name = "شعبه")]
    public int BranchId { get; set; }
    public Branch? Branch { get; set; }

    [Required(ErrorMessage = "نام مراجعه‌کننده الزامی است")]
    [MaxLength(150)]
    [Display(Name = "نام و نام خانوادگی")]
    public string VisitorName { get; set; } = string.Empty;

    [StringLength(10, MinimumLength = 10, ErrorMessage = "کد ملی باید ۱۰ رقم باشد")]
    [RegularExpression(@"^\d{10}$", ErrorMessage = "کد ملی فقط شامل ارقام است")]
    [Display(Name = "کد ملی")]
    public string? NationalCode { get; set; }

    [MaxLength(20)]
    [Display(Name = "شماره موبایل")]
    public string? Mobile { get; set; }

    [Required(ErrorMessage = "موضوع مراجعه الزامی است")]
    [MaxLength(300)]
    [Display(Name = "موضوع مراجعه")]
    public string Subject { get; set; } = string.Empty;

    [MaxLength(500)]
    [Display(Name = "توضیحات")]
    public string? Notes { get; set; }

    [Display(Name = "زمان ثبت")]
    public DateTime EntryTime { get; set; } = DateTime.Now;

    [Display(Name = "زمان حضور")]
    public DateTime? ArrivalTime { get; set; }

    [Display(Name = "آیا حضور تأیید شده؟")]
    public bool HasArrived => ArrivalTime.HasValue;

    [Display(Name = "زمان خروج")]
    public DateTime? ExitTime { get; set; }

    [Display(Name = "آیا خروج ثبت شده؟")]
    public bool HasExited => ExitTime.HasValue;

    [MaxLength(450)]
    [Display(Name = "ثبت‌کننده")]
    public string? RegisteredByUserId { get; set; }

    [MaxLength(150)]
    [Display(Name = "نام ثبت‌کننده")]
    public string? RegisteredByUserName { get; set; }
}
