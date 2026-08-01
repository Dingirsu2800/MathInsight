namespace MathInsight.Modules.Identity_Access.Entities;

public class TeacherApplication
{
    /// <summary>
    /// Separates the certificate URLs held in <see cref="DocumentsUrl"/> when an applicant
    /// uploaded more than one image (BR-05).
    /// </summary>
    public const string DocumentsUrlSeparator = "\n";

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