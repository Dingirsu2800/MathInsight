namespace MathInsight.Modules.Identity_Access.Contracts.Admin;

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
