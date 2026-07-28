using System.Text.Json;
using MathInsight.Modules.TestGen.Contracts.Tests;
using MathInsight.Modules.TestGen.Errors;
using MathInsight.Modules.TestGen.Generation;
using MathInsight.Modules.TestGen.Persistence;
using MathInsight.Shared.Questions;
using MathInsight.Shared.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace MathInsight.Modules.TestGen.Queries.GetExpertTestPreview;

public sealed class GetExpertTestPreviewQueryHandler
    : IRequestHandler<GetExpertTestPreviewQuery, Result<ExpertTestPreviewResponse>>
{
    private readonly TestGenDbContext _context;

    public GetExpertTestPreviewQueryHandler(TestGenDbContext context)
    {
        _context = context;
    }

    public async Task<Result<ExpertTestPreviewResponse>> Handle(
        GetExpertTestPreviewQuery query,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(query.ExpertId))
            return Result<ExpertTestPreviewResponse>.Failure(ApplicationErrors.AuthInvalidToken);
        if (string.IsNullOrWhiteSpace(query.TestId))
            return Result<ExpertTestPreviewResponse>.Failure(TestGenerationErrors.RequestInvalid);

        var test = await _context.Tests
            .AsNoTracking()
            .Include(item => item.Blueprint)
            .Include(item => item.Questions)
                .ThenInclude(question => question.QuestionVersion)
            .Include(item => item.Questions)
                .ThenInclude(question => question.SourceBlueprintDetail)
                    .ThenInclude(detail => detail!.BlueprintSection)
            .FirstOrDefaultAsync(item => item.TestId == query.TestId, cancellationToken);
        if (test is null ||
            test.TestMode != GeneratedTestValues.BlueprintExamMode ||
            test.GeneratedForStudentId is not null ||
            test.Blueprint is null ||
            test.TestCode is null)
        {
            return Result<ExpertTestPreviewResponse>.Failure(TestGenerationErrors.GeneratedTestNotFound);
        }

        if (!string.Equals(test.Blueprint.ExpertId, query.ExpertId, StringComparison.OrdinalIgnoreCase))
            return Result<ExpertTestPreviewResponse>.Failure(BlueprintErrors.MutationForbidden);

        var previewQuestions = new List<(ExpertTestPreviewQuestionResponse Response, string SectionId)>();
        foreach (var question in test.Questions.OrderBy(item => item.QuestionOrder))
        {
            if (question.QuestionVersion is null ||
                question.QuestionVersion.SnapshotSchemaVersion != 2 ||
                question.SourceBlueprintDetail?.BlueprintSection is null)
            {
                return Result<ExpertTestPreviewResponse>.Failure(TestGenerationErrors.QuestionVersionMissing);
            }

            QuestionSnapshotV2? snapshot;
            try
            {
                snapshot = JsonSerializer.Deserialize<QuestionSnapshotV2>(question.QuestionVersion.AnswersSnapshot);
            }
            catch (JsonException)
            {
                return Result<ExpertTestPreviewResponse>.Failure(TestGenerationErrors.QuestionVersionMissing);
            }

            if (snapshot is null || snapshot.Answers is null || snapshot.Parts is null)
                return Result<ExpertTestPreviewResponse>.Failure(TestGenerationErrors.QuestionVersionMissing);

            previewQuestions.Add((new ExpertTestPreviewQuestionResponse(
                question.QuestionId,
                question.QuestionVersionId,
                question.QuestionOrder,
                question.SourceBlueprintDetailId!,
                snapshot.QuestionType,
                snapshot.QuestionContent,
                snapshot.SolutionContent,
                snapshot.PictureUrl,
                question.WeightSnapshot,
                question.MaxPointsSnapshot,
                question.ScoringRuleSnapshot,
                snapshot.Answers.Select(answer => new ExpertPreviewAnswerResponse(
                    answer.AnswerId,
                    answer.AnswerContent,
                    answer.IsCorrect)).ToList(),
                snapshot.Parts.Select(part => new ExpertPreviewPartResponse(
                    part.PartId,
                    part.PartOrder,
                    part.PartLabel,
                    part.PartContent,
                    part.PartType,
                    part.CorrectBoolean,
                    part.CorrectText,
                    part.CorrectNumeric,
                    part.NumericTolerance,
                    part.Explanation,
                    part.DefaultWeight)).ToList()),
                question.SourceBlueprintDetail.BlueprintSectionId));
        }

        var sections = test.Questions
            .Select(question => question.SourceBlueprintDetail?.BlueprintSection)
            .Where(section => section is not null)
            .Cast<Persistence.Entities.BlueprintSection>()
            .DistinctBy(section => section.BlueprintSectionId)
            .OrderBy(section => section.SectionOrder)
            .Select(section => new ExpertTestPreviewSectionResponse(
                section.BlueprintSectionId,
                section.SectionOrder,
                section.SectionCode,
                section.SectionName,
                section.QuestionType,
                section.InstructionText,
                section.ScoreBudget,
                section.ScoringRule,
                previewQuestions
                    .Where(question => question.SectionId == section.BlueprintSectionId)
                    .Select(question => question.Response)
                    .ToList()))
            .ToList();

        return Result<ExpertTestPreviewResponse>.Success(new(
            test.TestId,
            test.BlueprintId!,
            test.TestName,
            test.TestCode,
            test.TestStatus,
            test.DurationMinutes,
            test.TotalQuestions,
            test.MaxScore,
            test.CreatedTime,
            sections));
    }
}
