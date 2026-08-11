using System.Text.Json;
using MathInsight.Modules.TestGen.Blueprints;
using MathInsight.Modules.TestGen.Contracts.Tests;
using MathInsight.Modules.TestGen.Errors;
using MathInsight.Modules.TestGen.Generation;
using MathInsight.Modules.TestGen.Persistence;
using MathInsight.Shared.Questions;
using MathInsight.Shared.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace MathInsight.Modules.TestGen.Queries.GetFixedTestCandidates;

public sealed class GetFixedTestCandidatesQueryHandler
    : IRequestHandler<GetFixedTestCandidatesQuery, Result<PagedFixedTestCandidateResponse>>
{
    private readonly TestGenDbContext _context;
    private readonly IBlueprintExamCandidateProvider _candidateProvider;

    public GetFixedTestCandidatesQueryHandler(TestGenDbContext context, IBlueprintExamCandidateProvider candidateProvider)
    {
        _context = context;
        _candidateProvider = candidateProvider;
    }

    public async Task<Result<PagedFixedTestCandidateResponse>> Handle(
        GetFixedTestCandidatesQuery query,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(query.ExpertId))
            return Result<PagedFixedTestCandidateResponse>.Failure(ApplicationErrors.AuthInvalidToken);
        if (string.IsNullOrWhiteSpace(query.BlueprintId) || string.IsNullOrWhiteSpace(query.BlueprintDetailId))
            return Result<PagedFixedTestCandidateResponse>.Failure(TestGenerationErrors.RequestInvalid);

        var blueprint = await _context.Blueprints.AsNoTracking()
            .Include(x => x.Sections).ThenInclude(x => x.Details)
            .FirstOrDefaultAsync(x => x.BlueprintId == query.BlueprintId, cancellationToken);
        if (blueprint is null || blueprint.Status == BlueprintStatuses.Deactivated)
            return Result<PagedFixedTestCandidateResponse>.Failure(BlueprintErrors.NotFound);
        if (!string.Equals(blueprint.ExpertId, query.ExpertId, StringComparison.OrdinalIgnoreCase))
            return Result<PagedFixedTestCandidateResponse>.Failure(BlueprintErrors.MutationForbidden);
        if (blueprint.Status is not (BlueprintStatuses.Approved or BlueprintStatuses.Active))
            return Result<PagedFixedTestCandidateResponse>.Failure(TestGenerationErrors.FixedTestBlueprintNotApproved);

        var requirement = BlueprintExamGenerationPlanner.BuildRequirements(blueprint)
            .SingleOrDefault(x => x.BlueprintDetailId == query.BlueprintDetailId);
        if (requirement is null)
            return Result<PagedFixedTestCandidateResponse>.Failure(TestGenerationErrors.FixedTestQuestionNotEligible);

        var pool = await _candidateProvider.GetCandidatesAsync(blueprint, cancellationToken);
        var eligible = pool.Candidates.Where(candidate => Matches(candidate, requirement)).ToList();
        var versionIds = eligible.Select(x => x.QuestionVersionId).ToList();
        var snapshots = await _context.QuestionVersions.AsNoTracking()
            .Where(x => versionIds.Contains(x.VersionId))
            .ToDictionaryAsync(x => x.VersionId, x => x.AnswersSnapshot, cancellationToken);

        var items = eligible.Select(candidate =>
        {
            QuestionSnapshotV2? snapshot = null;
            if (snapshots.TryGetValue(candidate.QuestionVersionId, out var json))
            {
                try { snapshot = JsonSerializer.Deserialize<QuestionSnapshotV2>(json); }
                catch (JsonException) { }
            }
            return snapshot is null ? null : new FixedTestCandidateResponse(
                candidate.QuestionId, candidate.QuestionVersionId, requirement.BlueprintDetailId,
                requirement.TagId, candidate.QuestionType, candidate.DifficultyId, candidate.PartCount,
                candidate.DefaultWeight, candidate.SupportedScoringRules.OrderBy(x => x).ToList(),
                snapshot.QuestionContent ?? string.Empty, snapshot.PictureUrl);
        }).Where(x => x is not null).Cast<FixedTestCandidateResponse>();

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var search = query.Search.Trim();
            items = items.Where(x => x.QuestionId.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                                     x.QuestionContent.Contains(search, StringComparison.OrdinalIgnoreCase));
        }

        var ordered = items.OrderBy(x => x.QuestionId, StringComparer.OrdinalIgnoreCase).ToList();
        var pageSize = query.PageSize <= 0 ? 20 : Math.Min(query.PageSize, 100);
        var pageIndex = query.PageIndex <= 0 ? 1 : Math.Min(query.PageIndex, int.MaxValue / pageSize);
        return Result<PagedFixedTestCandidateResponse>.Success(new(
            pageIndex, pageSize, ordered.Count,
            ordered.Skip((pageIndex - 1) * pageSize).Take(pageSize).ToList()));
    }

    private static bool Matches(BlueprintExamCandidate candidate, BlueprintExamRequirement requirement)
        => candidate.DifficultyId.Equals(requirement.DifficultyId, StringComparison.OrdinalIgnoreCase) &&
           candidate.QuestionType.Equals(requirement.QuestionType, StringComparison.OrdinalIgnoreCase) &&
           candidate.TagIds.Contains(requirement.TagId) &&
           candidate.SupportedScoringRules.Contains(requirement.ScoringRule) &&
           (requirement.PartCountPerQuestion is null || candidate.PartCount == requirement.PartCountPerQuestion);
}
