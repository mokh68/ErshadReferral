using System.ComponentModel.DataAnnotations;

namespace ErshadReferral.Models;

public enum ReferralImportAliasType
{
    Branch = 1,
    Subject = 2
}

public class ReferralImportAlias
{
    public int Id { get; set; }

    public ReferralImportAliasType Type { get; set; }

    [Required, MaxLength(300)]
    public string SourceValue { get; set; } = string.Empty;

    [Required, MaxLength(300)]
    public string NormalizedSourceValue { get; set; } = string.Empty;

    public int? TargetBranchId { get; set; }
    public Branch? TargetBranch { get; set; }

    [MaxLength(300)]
    public string? TargetSubject { get; set; }
}
