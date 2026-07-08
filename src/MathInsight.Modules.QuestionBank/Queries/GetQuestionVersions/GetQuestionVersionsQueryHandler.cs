using MathInsight.Modules.QuestionBank.Contracts.Questions;
using MathInsight.Modules.QuestionBank.Errors;
using MathInsight.Modules.QuestionBank.Persistence;
using MathInsight.Shared.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace MathInsight.Modules.QuestionBank.Queries.GetQuestionVersions;

public sealed class GetQuestionVersionsQueryHandler
    : IRequestHandler<GetQuestionVersionsQuery, Result<IReadOnlyList<QuestionVersionResponse>>>
{
    private readonly QuestionBankDbContext _context;

    public GetQuestionVersionsQueryHandler(QuestionBankDbContext context)
    {
        _context = context;
    }

    public async Task<Result<IReadOnlyList<QuestionVersionResponse>>> Handle(
        GetQuestionVersionsQuery request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.QuestionId))
            return Result<IReadOnlyList<QuestionVersionResponse>>.Failure(QuestionBankErrors.QuestionIdRequired);

        var questionExists = await _context.Questions
            .AsNoTracking()
            .AnyAsync(question => question.QuestionId == request.QuestionId, cancellationToken);

        if (!questionExists)
            return Result<IReadOnlyList<QuestionVersionResponse>>.Failure(QuestionBankErrors.QuestionNotFound);

        var versions = await _context.QuestionVersions
            .AsNoTracking()
            .Where(version => version.QuestionId == request.QuestionId)
            .OrderByDescending(version => version.CreatedTime)
            .Select(version => new QuestionVersionResponse(
                version.VersionId,
                version.QuestionId,
                version.QuestionContent,
                version.QuestionAnswer,
                version.AnswersSnapshot,
                version.PictureUrl,
                version.CreatedTime,
                version.ExpertId))
            .ToListAsync(cancellationToken);

        return Result<IReadOnlyList<QuestionVersionResponse>>.Success(versions);
    }
}
