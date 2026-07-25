using MediatR;

namespace MathInsight.Modules.Grading_Analytics.Queries.GetSessionResult;

/// <summary>
/// UC-55: Returns the full graded session result for a student.
/// Returns null when session is not found (controller maps to 404).
/// Throws UnauthorizedAccessException when student does not own the session (controller maps to 403).
/// </summary>
public sealed record GetSessionResultQuery(
    string SessionId,
    string AuthenticatedStudentId) : IRequest<SessionResultDto?>;
