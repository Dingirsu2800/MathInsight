using MathInsight.Modules.Identity_Access.Contracts.Admin;
using MathInsight.Modules.Identity_Access.Contracts.Common;
using MathInsight.Shared.Results;
using MediatR;

namespace MathInsight.Modules.Identity_Access.Queries.GetAccountList;

public sealed record GetAccountListQuery(
    int PageIndex,
    int PageSize,
    string? RoleName,
    bool? IsActive,
    string? Search) : IRequest<Result<PagedResponse<AccountListItemResponse>>>;
