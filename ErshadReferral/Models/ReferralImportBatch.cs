using System.ComponentModel.DataAnnotations;

namespace ErshadReferral.Models;

public class ReferralImportBatch
{
    public int Id { get; set; }

    [Required, MaxLength(64)]
    public string FileSha256 { get; set; } = string.Empty;

    [Required, MaxLength(260)]
    public string FileName { get; set; } = string.Empty;

    public DateTime ImportedAt { get; set; }

    [MaxLength(256)]
    public string? UploadedByUserName { get; set; }

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
