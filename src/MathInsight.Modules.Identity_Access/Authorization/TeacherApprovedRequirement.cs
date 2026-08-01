using Microsoft.AspNetCore.Authorization;

namespace MathInsight.Modules.Identity_Access.Authorization;

/// <summary>
/// BR-06. The caller must hold the Teacher role AND have an approved teacher application.
///
/// A Teacher account exists — and carries the Teacher role — from email confirmation onward,
/// before an Admin has reviewed anything (see ConfirmEmailCommandHandler). The role claim alone
/// therefore proves nothing about approval, and this requirement is what separates a working
/// teacher from an applicant.
/// </summary>
public sealed class TeacherApprovedRequirement : IAuthorizationRequirement;
