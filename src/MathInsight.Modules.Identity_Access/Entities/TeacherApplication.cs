namespace MathInsight.Modules.Identity_Access.Entities;

public class TeacherApplication
{
    public string ApplicationId { get; set; } = default!;
    public string TeacherId { get; set; } = default!;
    public string DocumentsUrl { get; set; } = default!;
    public string Status { get; set; } = default!;
    public string? ReviewComments { get; set; }
    public DateTime AppliedTime { get; set; }
    public DateTime? ReviewedTime { get; set; }
    public string? ReviewedBy { get; set; }

    public Teacher Teacher { get; set; } = default!;
    public Account? Reviewer { get; set; }
}