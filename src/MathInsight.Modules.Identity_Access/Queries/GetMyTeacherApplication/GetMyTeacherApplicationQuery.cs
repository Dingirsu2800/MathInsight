using MathInsight.Modules.Identity_Access.Contracts.Teacher;
using MathInsight.Shared.Results;
using MediatR;

namespace MathInsight.Modules.Identity_Access.Queries.GetMyTeacherApplication;

/// <summary>
/// UC-08. Reads the caller's own most recent application. There is deliberately no application-id
/// parameter: the row is resolved from <paramref name="AccountId"/>, which comes from the access
/// token, so this query cannot be pointed at another teacher's application.
/// </summary>
public sealed record GetMyTeacherApplicationQuery(string AccountId)
    : IRequest<Result<MyTeacherApplicationResponse>>;
