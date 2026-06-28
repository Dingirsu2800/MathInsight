namespace MathInsight.Modules.Identity_Access.Entities;

public class Student
{
    public string StudentId { get; set; } = default!;
    public string? Gender { get; set; }
    public string? School { get; set; }
    public int? CurrentGrade { get; set; }

    public Account Account { get; set; } = default!;
}