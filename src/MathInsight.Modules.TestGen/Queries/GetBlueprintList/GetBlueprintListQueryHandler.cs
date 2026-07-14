using MathInsight.Modules.TestGen.Blueprints;
using MathInsight.Modules.TestGen.Contracts.Blueprints;
using MathInsight.Modules.TestGen.Contracts.Common;
using MathInsight.Modules.TestGen.Errors;
using MathInsight.Modules.TestGen.Persistence;
using MathInsight.Shared.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace MathInsight.Modules.TestGen.Queries.GetBlueprintList;

public sealed class GetBlueprintListQueryHandler
    : IRequestHandler<GetBlueprintListQuery, Result<PagedResponse<BlueprintListItemResponse>>>
{
    private const int DefaultPageIndex = 1;
    private const int DefaultPageSize = 20;
    private const int MaxPageSize = 100;

    private readonly TestGenDbContext _context;

    public GetBlueprintListQueryHandler(TestGenDbContext context)
    {
        _context = context;
    }

    public async Task<Result<PagedResponse<BlueprintListItemResponse>>> Handle(
        GetBlueprintListQuery request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.CurrentExpertId))
            return Result<PagedResponse<BlueprintListItemResponse>>.Failure(ApplicationErrors.AuthInvalidToken);

        var status = BlueprintStatuses.Normalize(request.Status);
        if (status == string.Empty || request.Grade is not null and not (10 or 11 or 12))
            return Result<PagedResponse<BlueprintListItemResponse>>.Failure(BlueprintErrors.RequestInvalid);

        var pageIndex = request.PageIndex <= 0 ? DefaultPageIndex : request.PageIndex;
        var pageSize = request.PageSize <= 0 ? DefaultPageSize : Math.Min(request.PageSize, MaxPageSize);
        var query = _context.Blueprints.AsNoTracking().AsQueryable();

        query = request.IncludeDeactivated
            ? query.Where(blueprint =>
                blueprint.Status != BlueprintStatuses.Deactivated ||
                blueprint.ExpertId == request.CurrentExpertId)
            : query.Where(blueprint => blueprint.Status != BlueprintStatuses.Deactivated);

        if (status is not null)
            query = query.Where(blueprint => blueprint.Status == status);

        if (request.Grade is not null)
            query = query.Where(blueprint => blueprint.Grade == request.Grade);

        if (!string.IsNullOrWhiteSpace(request.ExpertId))
        {
            var expertId = request.ExpertId.Trim();
            query = query.Where(blueprint => blueprint.ExpertId == expertId);
        }

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var search = request.Search.Trim();
            query = query.Where(blueprint => blueprint.BlueprintName.Contains(search));
        }

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
