using MathInsight.Modules.Identity_Access.Contracts.Admin;
using MathInsight.Modules.Identity_Access.Contracts.Common;
using MathInsight.Shared.Results;
using MediatR;

namespace MathInsight.Modules.Identity_Access.Queries.GetTeacherApplications;

public sealed record GetTeacherApplicationsQuery(
    int PageIndex,
    int PageSize,
    string? Status) : IRequest<Result<PagedResponse<TeacherApplicationListItemResponse>>>;
