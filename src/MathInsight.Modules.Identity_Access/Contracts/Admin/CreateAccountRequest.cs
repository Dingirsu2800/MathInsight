using System.ComponentModel.DataAnnotations;

namespace MathInsight.Modules.Identity_Access.Contracts.Admin;

public sealed class CreateAccountRequest : IValidatableObject
{
    [Required, MaxLength(50)]
    public string Username { get; set; } = default!;

    [Required, MaxLength(100)]
    public string Email { get; set; } = default!;

    [Required, MinLength(8), MaxLength(128)]
    public string Password { get; set; } = default!;

    [Required, MaxLength(50)]
    public string FirstName { get; set; } = default!;

    [Required, MaxLength(50)]
    public string LastName { get; set; } = default!;

    [MaxLength(20)]
    public string? PhoneNumber { get; set; }

    public DateOnly? DateOfBirth { get; set; }

    [Range(10, 12)]
    public int? CurrentGrade { get; set; }

    /// <summary>One of: Student, Teacher, Expert.</summary>
    [Required]
    public string RoleName { get; set; } = default!;

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (!string.IsNullOrWhiteSpace(PhoneNumber) && !System.Text.RegularExpressions.Regex.IsMatch(
                PhoneNumber.Trim(), Contracts.Auth.AuthValidation.PhoneNumberPattern))
        {
            yield return new ValidationResult("Số điện thoại không hợp lệ.", new[] { nameof(PhoneNumber) });
        }

        if (DateOfBirth is DateOnly dateOfBirth)
        {
            var today = DateOnly.FromDateTime(DateTime.Today);
            if (dateOfBirth > today)
                yield return new ValidationResult("Ngày sinh không được lớn hơn ngày hiện tại.", new[] { nameof(DateOfBirth) });
            else if (string.Equals(RoleName, "Student", StringComparison.OrdinalIgnoreCase) && CurrentGrade is >= 10 and <= 12)
            {
                var age = today.Year - dateOfBirth.Year - (today.DayOfYear < dateOfBirth.DayOfYear ? 1 : 0);
                if (age < 14 || age > 20)
                    yield return new ValidationResult("Ngày sinh không phù hợp với độ tuổi học sinh THPT.", new[] { nameof(DateOfBirth) });
            }
        }
    }
}
