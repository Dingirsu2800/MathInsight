using MathInsight.Modules.Identity_Access.Contracts.Admin;
using MathInsight.Shared.Results;
using MediatR;

namespace MathInsight.Modules.Identity_Access.Queries.GetTeacherApplicationDetail;

public sealed record GetTeacherApplicationDetailQuery(string ApplicationId)
    : IRequest<Result<TeacherApplicationDetailResponse>>;
