namespace MathInsight.Modules.Identity_Access.Entities;

public class Teacher
{
    public string TeacherId { get; set; } = default!;
    public string? Biography { get; set; }
    public bool IsVerified { get; set; }
    public string? CccdNumber { get; set; }

    public Account Account { get; set; } = default!;
    public ICollection<TeacherApplication> TeacherApplications { get; set; } = new List<TeacherApplication>();
}