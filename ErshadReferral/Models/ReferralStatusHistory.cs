using System.ComponentModel.DataAnnotations;

namespace ErshadReferral.Models;

public class ReferralStatusHistory
{
    public int Id { get; set; }

    public int ReferralId { get; set; }
    public Referral? Referral { get; set; }

    [Display(Name = "وضعیت")]
    public ReferralStatus Status { get; set; }

    [MaxLength(500)]
    [Display(Name = "توضیح")]
    public string? Note { get; set; }

    [Display(Name = "تاریخ ثبت")]
    public DateTime ChangedAt { get; set; } = DateTime.Now;

    [MaxLength(450)]
    public string? ChangedByUserId { get; set; }

    [MaxLength(150)]
    [Display(Name = "کاربر")]
    public string? ChangedByUserName { get; set; }

    [MaxLength(50)]
    [Display(Name = "نقش کاربر")]
    public string? ChangedByRole { get; set; }
}
