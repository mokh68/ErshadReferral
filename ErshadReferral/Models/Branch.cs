using System.ComponentModel.DataAnnotations;

namespace ErshadReferral.Models;

public class Branch
{
    public int Id { get; set; }

    [Required(ErrorMessage = "شماره/نام شعبه الزامی است")]
    [MaxLength(100)]
    [Display(Name = "شماره/نام شعبه")]
    public string Name { get; set; } = string.Empty;

    [MaxLength(150)]
    [Display(Name = "نام رئیس شعبه / قاضی")]
    public string? JudgeName { get; set; }

    [MaxLength(50)]
    [Display(Name = "تلفن داخلی")]
    public string? Phone { get; set; }

    [Display(Name = "دادگاه")]
    public int CourtId { get; set; }
    public Court? Court { get; set; }

    [Display(Name = "فعال")]
    public bool IsActive { get; set; } = true;

    public ICollection<Referral> Referrals { get; set; } = new List<Referral>();
}
