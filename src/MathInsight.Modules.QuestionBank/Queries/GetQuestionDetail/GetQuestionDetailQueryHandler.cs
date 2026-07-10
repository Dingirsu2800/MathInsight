using MathInsight.Modules.QuestionBank.Contracts.Questions;
using MathInsight.Modules.QuestionBank.Errors;
using MathInsight.Modules.QuestionBank.Persistence;
using MathInsight.Shared.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace MathInsight.Modules.QuestionBank.Queries.GetQuestionDetail;

public sealed class GetQuestionDetailQueryHandler
    : IRequestHandler<GetQuestionDetailQuery, Result<QuestionDetailResponse>>
{
    private readonly QuestionBankDbContext _context;

    public GetQuestionDetailQueryHandler(QuestionBankDbContext context)
    {
        _context = context;
    }

    public async Task<Result<QuestionDetailResponse>> Handle(
        GetQuestionDetailQuery request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.QuestionId))
            return Result<QuestionDetailResponse>.Failure(QuestionBankErrors.QuestionIdRequired);

        var question = await _context.Questions
            .AsNoTracking()
            .Where(question => question.QuestionId == request.QuestionId)
            .Select(question => new QuestionDetailResponse(
                question.QuestionId,
                question.QuestionContent,
                question.SolutionContent,
                question.PictureUrl,
                question.DifficultyId,
                question.Difficulty.DifficultyName,
                question.Difficulty.LevelValue,
                question.Grade,
                question.Status,
                question.QuestionType,
                question.ExpertId,
                _context.AccountReadModels
                    .Where(account => account.AccountId == question.ExpertId)
                    .Select(account => account.FirstName + " " + account.LastName)
                    .FirstOrDefault(),
                question.DefaultPoint,
                question.IsActive,
                question.QuestionTopics
                    .OrderByDescending(topic => topic.IsPrimary)
                    .ThenBy(topic => topic.Tag.DisplayOrder)
                    .Select(topic => new QuestionTopicResponse(
                        topic.TagId,
                        topic.Tag.TagName,
                        topic.IsPrimary))
                    .ToList(),
                question.Answers
                    .OrderBy(answer => answer.AnswerId)
                    .Select(answer => new QuestionAnswerResponse(
                        answer.AnswerId,
                        answer.AnswerContent,
                        answer.IsCorrect))
                    .ToList(),
                question.Parts
                    .OrderBy(part => part.PartOrder)
                    .Select(part => new QuestionPartResponse(
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
                        part.DefaultPoint))
                    .ToList()))
            .FirstOrDefaultAsync(cancellationToken);

        if (question is null)
            return Result<QuestionDetailResponse>.Failure(QuestionBankErrors.QuestionNotFound);

        return Result<QuestionDetailResponse>.Success(question);
    }
}
