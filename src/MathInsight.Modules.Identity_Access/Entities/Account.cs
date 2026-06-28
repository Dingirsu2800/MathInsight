namespace MathInsight.Modules.Identity_Access.Entities;

public class Account
{
    public string AccountId { get; set; } = default!;
    public string Username { get; set; } = default!;
    public string PasswordHash { get; set; } = default!;
    public string Email { get; set; } = default!;
    public string FirstName { get; set; } = default!;
    public string LastName { get; set; } = default!;
    public string? PhoneNumber { get; set; }
    public DateOnly? DateOfBirth { get; set; }
    public string? AvatarUrl { get; set; }
    public string RoleId { get; set; } = default!;
    public bool IsActive { get; set; }
    public DateTime CreatedTime { get; set; }
    public string? GoogleSubId { get; set; }
    public string? GoogleEmail { get; set; }

    public Role Role { get; set; } = default!;
    public Student? Student { get; set; }
    public Teacher? Teacher { get; set; }
    public Expert? Expert { get; set; }
}

