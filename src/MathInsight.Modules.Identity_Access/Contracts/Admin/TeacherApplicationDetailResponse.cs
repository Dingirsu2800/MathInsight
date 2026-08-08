namespace MathInsight.Modules.Identity_Access.Contracts.Admin;

/// <summary>
/// <paramref name="DocumentsUrl"/> carries one or more certificate URLs separated by
/// <see cref="Entities.TeacherApplication.DocumentsUrlSeparator"/> (BR-05 allows multiple uploads).
/// </summary>
public sealed record TeacherApplicationDetailResponse(
    string ApplicationId,
    string TeacherId,
    string TeacherFullName,
    string TeacherEmail,
    string? TeacherPhoneNumber,
    string? Biography,
    string DocumentsUrl,
    string Status,
    string? ReviewComments,
    DateTime AppliedTime,
    DateTime? ReviewedTime,
    string? ReviewedBy);
