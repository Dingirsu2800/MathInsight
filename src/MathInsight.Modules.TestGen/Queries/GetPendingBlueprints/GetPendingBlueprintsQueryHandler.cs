using MathInsight.Modules.TestGen.Blueprints;
using MathInsight.Modules.TestGen.Contracts.Blueprints;
using MathInsight.Modules.TestGen.Contracts.Common;
using MathInsight.Modules.TestGen.Persistence;
using MathInsight.Shared.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace MathInsight.Modules.TestGen.Queries.GetPendingBlueprints;

public sealed class GetPendingBlueprintsQueryHandler
    : IRequestHandler<GetPendingBlueprintsQuery, Result<PagedResponse<BlueprintListItemResponse>>>
{
    private const int DefaultPageIndex = 1;
    private const int DefaultPageSize = 20;
    private const int MaxPageSize = 100;

    private readonly TestGenDbContext _context;

    public GetPendingBlueprintsQueryHandler(TestGenDbContext context)
    {
        _context = context;
    }

    public async Task<Result<PagedResponse<BlueprintListItemResponse>>> Handle(
        GetPendingBlueprintsQuery request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.CurrentExpertId))
            return Result<PagedResponse<BlueprintListItemResponse>>.Failure(ApplicationErrors.AuthInvalidToken);

        var pageIndex = request.PageIndex <= 0 ? DefaultPageIndex : request.PageIndex;
        var pageSize = request.PageSize <= 0 ? DefaultPageSize : Math.Min(request.PageSize, MaxPageSize);
        var query = _context.Blueprints
            .AsNoTracking()
            .Where(blueprint =>
                blueprint.Status == BlueprintStatuses.PendingReview &&
                blueprint.ExpertId != request.CurrentExpertId);

        var totalCount = await query.CountAsync(cancellationToken);
        var totalPages = totalCount == 0
            ? 0
            : (int)Math.Ceiling(totalCount / (double)pageSize);

        var items = await query
            .OrderBy(blueprint => blueprint.BlueprintName)
            .ThenBy(blueprint => blueprint.BlueprintId)
            .Skip((pageIndex - 1) * pageSize)
            .Take(pageSize)
            .Select(blueprint => new BlueprintListItemResponse(
                blueprint.BlueprintId,
                blueprint.BlueprintName,
                blueprint.Grade,
                blueprint.TotalQuestions,
                blueprint.DurationMinutes,
                blueprint.ExpertId,
                _context.Accounts
                    .Where(account => account.AccountId == blueprint.ExpertId)
                    .Select(account => account.FirstName + " " + account.LastName)
                    .FirstOrDefault(),
                blueprint.Status,
                blueprint.Sections.Count,
                blueprint.Sections.SelectMany(section => section.Details).Count()))
            .ToListAsync(cancellationToken);

        return Result<PagedResponse<BlueprintListItemResponse>>.Success(
            new PagedResponse<BlueprintListItemResponse>(
                items,
                pageIndex,
                pageSize,
                totalCount,
                totalPages));
    }
}
