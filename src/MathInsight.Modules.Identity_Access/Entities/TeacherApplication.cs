namespace MathInsight.Modules.Identity_Access.Entities;

public class TeacherApplication
{
    /// <summary>
    /// Separates the certificate URLs held in <see cref="DocumentsUrl"/> when an applicant
    /// uploaded more than one image (BR-05).
    /// </summary>
    public const string DocumentsUrlSeparator = "\n";

    // The three values permitted by CK_TeacherApplication_Status, in the exact casing the DB
    // stores. Comparisons elsewhere are OrdinalIgnoreCase, but writes must use these.
    /// <summary>Matches the DocumentsUrl column width (004_Alter_TeacherApplication_DocumentsUrl.sql).</summary>
    public const int DocumentsUrlMaxLength = 2000;

    public const string StatusPending = "Pending";
    public const string StatusApproved = "Approved";
    public const string StatusRejected = "Rejected";

    public string ApplicationId { get; set; } = default!;
    public string TeacherId { get; set; } = default!;

    /// <summary>
    /// One or more certificate image URLs, separated by <see cref="DocumentsUrlSeparator"/>.
    /// </summary>
    public string DocumentsUrl { get; set; } = default!;
    public string Status { get; set; } = default!;
    public string? ReviewComments { get; set; }
    public DateTime AppliedTime { get; set; }
    public DateTime? ReviewedTime { get; set; }
    public string? ReviewedBy { get; set; }

    public Teacher Teacher { get; set; } = default!;
    public Account? Reviewer { get; set; }
}