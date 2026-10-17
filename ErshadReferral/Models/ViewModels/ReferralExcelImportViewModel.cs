using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;

namespace ErshadReferral.Models.ViewModels;

public class ReferralExcelImportViewModel
{
    [Required(ErrorMessage = "انتخاب فایل اکسل الزامی است")]
    [Display(Name = "فایل اکسل")]
    public IFormFile? File { get; set; }

    public string? ErrorMessage { get; set; }
    public ReferralExcelImportSummary? Summary { get; set; }
}

public class ReferralExcelImportSummary
{
    public int TotalRows { get; set; }
    public int ImportedRows { get; set; }
    public int SkippedDuplicateRows { get; set; }
    public int NewPeople { get; set; }
    public int ReusedPeople { get; set; }
    public int MappedBranchRows { get; set; }
    public int UnmappedBranchRows { get; set; }
    public int MappedSubjectRows { get; set; }
    public int UnmappedSubjectRows { get; set; }
    public int InvalidNationalCodeRows { get; set; }
    public int MissingLastNameRows { get; set; }
}
