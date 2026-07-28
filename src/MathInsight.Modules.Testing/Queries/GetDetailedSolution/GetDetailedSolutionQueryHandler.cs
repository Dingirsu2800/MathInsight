using MathInsight.Modules.Testing.Contracts;
using MathInsight.Modules.Testing.Errors;
using MathInsight.Modules.Testing.Persistence;
using MathInsight.Shared.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace MathInsight.Modules.Testing.Queries.GetDetailedSolution;

public sealed class GetDetailedSolutionQueryHandler
    : IRequestHandler<GetDetailedSolutionQuery, Result<DetailedSolutionResponse>>
{
    private readonly TestingDbContext _db;

    public GetDetailedSolutionQueryHandler(TestingDbContext db)
    {
        _db = db;
    }

    public async Task<Result<DetailedSolutionResponse>> Handle(
        GetDetailedSolutionQuery request,
        CancellationToken cancellationToken)
    {
        // 1. Load session
        var session = await _db.TestSessions
            .Include(s => s.Test)
            .FirstOrDefaultAsync(s => s.SessionId == request.SessionId, cancellationToken);

        if (session is null)
            return Result<DetailedSolutionResponse>.Failure(TestingErrors.SessionNotFound);

        // Validate the session belongs to the requesting student
        if (session.StudentId != request.StudentId)
            return Result<DetailedSolutionResponse>.Failure(TestingErrors.SessionNotFound);

        // 2. Validate session Status = Graded (reject with 403 equivalent if not)
        if (session.Status != "Graded")
            return Result<DetailedSolutionResponse>.Failure(TestingErrors.SessionNotGraded);

        // 3. Load all answers with options and parts
        var answers = await _db.TestAnswers
            .Include(a => a.Options)
            .Include(a => a.Parts)
            .Where(a => a.SessionId == request.SessionId)
            .OrderBy(a => a.QuestionNo)
            .ToListAsync(cancellationToken);

        // 4. Map to DTOs
        var questionDtos = answers.Select(a => new SolutionQuestionDto(
            QuestionId: a.QuestionId,
            QuestionNo: a.QuestionNo,
            IsCorrect: a.IsCorrect,
            PointsEarned: a.PointsEarned,
            ShortAnswerText: a.ShortAnswerText,
            SelectedAnswerId: a.AnswerId,
            SelectedOptions: a.Options
                .Select(o => new SolutionOptionDto(o.AnswerId))
                .ToList(),
            Parts: a.Parts
                .Select(p => new SolutionPartDto(
                    PartId: p.PartId,
                    BooleanAnswer: p.BooleanAnswer,
                    TextAnswer: p.TextAnswer,
                    NumericAnswer: p.NumericAnswer,
                    IsCorrect: p.IsCorrect,
                    PointsEarned: p.PointsEarned))
                .ToList()
        )).ToList();

        var response = new DetailedSolutionResponse(
            SessionId: session.SessionId,
            TestName: session.Test?.TestName ?? string.Empty,
            Score: session.Score,
            NumCorrect: session.NumCorrect,
            NumIncorrect: session.NumIncorrect,
            NumAbandoned: session.NumAbandoned,
            Questions: questionDtos);

        return Result<DetailedSolutionResponse>.Success(response);
    }
}
