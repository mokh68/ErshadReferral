using System.ComponentModel.DataAnnotations;

namespace ErshadReferral.Models.ViewModels;

public class VisitorEntryCreateViewModel
{
    [Required(ErrorMessage = "انتخاب شعبه الزامی است")]
    [Display(Name = "شعبه")]
    public int BranchId { get; set; }

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
}

public class VisitorFilterViewModel
{
    [Display(Name = "نام مراجعه‌کننده")]
    public string? VisitorName { get; set; }

    [Display(Name = "کد ملی")]
    public string? NationalCode { get; set; }

    [Display(Name = "شعبه")]
    public int? BranchId { get; set; }

    [Display(Name = "از تاریخ")]
    public string? FromDate { get; set; }

    [Display(Name = "تا تاریخ")]
    public string? ToDate { get; set; }

    [Display(Name = "فقط در حال مراجعه")]
    public bool? OnlyActive { get; set; }
}

public class OfficeManagerDashboardViewModel
{
    public string BranchName { get; set; } = string.Empty;
    public int TodayVisitorCount { get; set; }
    public int WaitingVisitorsCount { get; set; }
    public int ArrivedVisitorsCount { get; set; }
    public int ExitedVisitorsCount { get; set; }
    public int WaitingReferralsCount { get; set; }
    public int ActiveReferralsCount { get; set; }
    public int ClosedReferralsCount { get; set; }
    public List<VisitorEntry> WaitingVisitors { get; set; } = new();
    public List<VisitorEntry> ArrivedVisitors { get; set; } = new();
    public List<VisitorEntry> ExitedVisitors { get; set; } = new();
    public List<Referral> WaitingReferrals { get; set; } = new();
    public List<Referral> ActiveReferrals { get; set; } = new();
    public List<Referral> ClosedReferrals { get; set; } = new();
}
