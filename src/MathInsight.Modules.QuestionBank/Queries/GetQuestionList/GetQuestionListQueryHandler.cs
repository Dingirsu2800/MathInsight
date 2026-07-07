using MathInsight.Modules.QuestionBank.Contracts.Common;
using MathInsight.Modules.QuestionBank.Contracts.Questions;
using MathInsight.Modules.QuestionBank.Errors;
using MathInsight.Modules.QuestionBank.Persistence;
using MathInsight.Shared.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace MathInsight.Modules.QuestionBank.Queries.GetQuestionList;

public class GetQuestionListQueryHandler
    : IRequestHandler<GetQuestionListQuery, Result<PagedResponse<QuestionListItemResponse>>>
{
    private const int DefaultPageIndex = 1;
    private const int DefaultPageSize = 20;
    private const int MaxPageSize = 100;

    private readonly QuestionBankDbContext _context;

    public GetQuestionListQueryHandler(QuestionBankDbContext context)
    {
        _context = context;
    }

    public async Task<Result<PagedResponse<QuestionListItemResponse>>> Handle(
        GetQuestionListQuery request,
        CancellationToken cancellationToken)
    {
        var pageIndex = request.PageIndex <= 0 ? DefaultPageIndex : request.PageIndex;
        var pageSize = request.PageSize <= 0 ? DefaultPageSize : Math.Min(request.PageSize, MaxPageSize);

        var status = NormalizeStatus(request.Status);
        if (status == string.Empty)
            return Result<PagedResponse<QuestionListItemResponse>>.Failure(QuestionBankErrors.QuestionStatusInvalid);

        var questionType = MapQuestionType(request.QuestionType);
        if (questionType == string.Empty)
            return Result<PagedResponse<QuestionListItemResponse>>.Failure(QuestionBankErrors.QuestionInvalidType);

        var query = _context.Questions
            .AsNoTracking()
            .AsQueryable();

        if (status is not null)
            query = query.Where(question => question.Status == status);

        if (request.Grade is not null)
            query = query.Where(question => question.Grade == request.Grade);

        if (!string.IsNullOrWhiteSpace(request.DifficultyId))
            query = query.Where(question => question.DifficultyId == request.DifficultyId);

        if (!string.IsNullOrWhiteSpace(request.ExpertId))
            query = query.Where(question => question.ExpertId == request.ExpertId);

        if (questionType is not null)
            query = query.Where(question => question.QuestionType == questionType);

        if (!string.IsNullOrWhiteSpace(request.TagId))
            query = query.Where(question => question.QuestionTopics.Any(topic => topic.TagId == request.TagId));

        var totalCount = await query.CountAsync(cancellationToken);
        var totalPages = totalCount == 0
            ? 0
            : (int)Math.Ceiling(totalCount / (double)pageSize);

        var items = await query
            .OrderBy(question => question.QuestionId)
            .Skip((pageIndex - 1) * pageSize)
            .Take(pageSize)
            .Select(question => new QuestionListItemResponse(
                question.QuestionId,
                question.QuestionContent,
                question.PictureUrl,
                question.DifficultyId,
                question.Difficulty.DifficultyName,
                question.Difficulty.LevelValue,
                question.Grade,
                question.Status,
                question.QuestionType,
                question.ExpertId,
                question.DefaultPoint,
                question.IsActive,
                question.QuestionTopics
                    .OrderByDescending(topic => topic.IsPrimary)
                    .ThenBy(topic => topic.Tag.DisplayOrder)
                    .Select(topic => new QuestionTopicSummaryResponse(
                        topic.TagId,
                        topic.Tag.TagName,
                        topic.IsPrimary))
                    .ToList()))
            .ToListAsync(cancellationToken);

        return Result<PagedResponse<QuestionListItemResponse>>.Success(
            new PagedResponse<QuestionListItemResponse>(
                items,
                pageIndex,
                pageSize,
                totalCount,
                totalPages));
    }

    private static string? NormalizeStatus(string? status)
    {
        if (string.IsNullOrWhiteSpace(status))
            return null;

        return status.Trim().ToUpperInvariant() switch
        {
            "APPROVED" => "Approved",
            "REPORTED" => "Reported",
            "REJECTED" => "Rejected",
            "DEACTIVATED" => "Deactivated",
            _ => string.Empty
        };
    }

    private static string? MapQuestionType(string? questionType)
    {
        if (string.IsNullOrWhiteSpace(questionType))
            return null;

        return questionType.Trim().ToUpperInvariant() switch
        {
            "SINGLE_CHOICE" => "SingleChoice",
            "MULTIPLE_CHOICE" => "MultipleChoice",
            "MULTIPLE_SELECT" => "MultipleChoice",
            "TRUE_FALSE" => "TrueFalse",
            "SHORT_ANSWER" => "ShortAnswer",
            "COMPOSITE" => "Composite",
            "SINGLECHOICE" => "SingleChoice",
            "MULTIPLECHOICE" => "MultipleChoice",
            "TRUEFALSE" => "TrueFalse",
            "SHORTANSWER" => "ShortAnswer",
            _ => string.Empty
        };
    }
}
