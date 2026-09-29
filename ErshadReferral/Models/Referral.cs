using System.ComponentModel.DataAnnotations;

namespace ErshadReferral.Models;

public enum ReferralStatus
{
    [Display(Name = "ارجاع‌شده")] Referred = 1,
    [Display(Name = "وصول در شعبه")] Received = 2,
    [Display(Name = "در حال رسیدگی")] InProgress = 3,
    [Display(Name = "مختومه")] Closed = 4,
    [Display(Name = "اعاده به ارشاد")] ReturnedToErshad = 5,
    [Display(Name = "ابطال‌شده")] Cancelled = 6
}

public class Referral
{
    public int Id { get; set; }

    [MaxLength(20)]
    [Display(Name = "شماره ارجاع")]
    public string ReferralNumber { get; set; } = string.Empty;

    [Display(Name = "مراجع")]
    public int PersonId { get; set; }
    public Person? Person { get; set; }

    [Display(Name = "شعبه مقصد")]
    public int BranchId { get; set; }
    public Branch? Branch { get; set; }

    [Required(ErrorMessage = "موضوع الزامی است")]
    [MaxLength(300)]
    [Display(Name = "موضوع / خلاصه خواسته")]
    public string Subject { get; set; } = string.Empty;

    [MaxLength(1000)]
    [Display(Name = "شرح")]
    public string? Description { get; set; }

    [Display(Name = "تاریخ ارجاع")]
    public DateTime ReferralDate { get; set; } = DateTime.Now;

    [Display(Name = "وضعیت فعلی")]
    public ReferralStatus Status { get; set; } = ReferralStatus.Referred;

    [MaxLength(450)]
    [Display(Name = "ثبت‌کننده")]
    public string? CreatedByUserId { get; set; }

    [MaxLength(150)]
    [Display(Name = "نام ثبت‌کننده")]
    public string? CreatedByUserName { get; set; }

    public ICollection<ReferralStatusHistory> StatusHistories { get; set; } = new List<ReferralStatusHistory>();
}
