namespace MathInsight.Modules.Identity_Access.Contracts.Admin;

public sealed record TeacherApplicationListItemResponse(
    string ApplicationId,
    string TeacherId,
    string TeacherFullName,
    string TeacherEmail,
    string Status,
    DateTime AppliedTime);
