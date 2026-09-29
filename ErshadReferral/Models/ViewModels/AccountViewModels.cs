using System.ComponentModel.DataAnnotations;

namespace ErshadReferral.Models.ViewModels;

public class LoginViewModel
{
    [Required(ErrorMessage = "نام کاربری الزامی است")]
    [Display(Name = "نام کاربری")]
    public string UserName { get; set; } = string.Empty;

    [Required(ErrorMessage = "رمز عبور الزامی است")]
    [DataType(DataType.Password)]
    [Display(Name = "رمز عبور")]
    public string Password { get; set; } = string.Empty;

    [Display(Name = "مرا به خاطر بسپار")]
    public bool RememberMe { get; set; }
}

public class UserFormViewModel
{
    public string? Id { get; set; }

    [Required(ErrorMessage = "نام کاربری الزامی است")]
    [Display(Name = "نام کاربری")]
    public string UserName { get; set; } = string.Empty;

    [Required(ErrorMessage = "نام و نام خانوادگی الزامی است")]
    [Display(Name = "نام و نام خانوادگی")]
    public string FullName { get; set; } = string.Empty;

    [DataType(DataType.Password)]
    [Display(Name = "رمز عبور")]
    public string? Password { get; set; }

    [Required(ErrorMessage = "نقش الزامی است")]
    [Display(Name = "نقش")]
    public string Role { get; set; } = string.Empty;

    [Display(Name = "شعبه")]
    public int? BranchId { get; set; }

    [Display(Name = "فعال")]
    public bool IsActive { get; set; } = true;
}
