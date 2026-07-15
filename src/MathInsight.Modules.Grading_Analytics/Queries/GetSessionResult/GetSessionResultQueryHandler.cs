using MediatR;
using Microsoft.EntityFrameworkCore;
using MathInsight.Modules.Grading_Analytics.Persistence;

namespace MathInsight.Modules.Grading_Analytics.Queries.GetSessionResult;

/// <summary>
/// Handles GetSessionResultQuery (UC-55).
/// Loads session + all nested navigation properties required for the result page.
/// </summary>
public sealed class GetSessionResultQueryHandler
    : IRequestHandler<GetSessionResultQuery, SessionResultDto?>
{
    private readonly GradingDbContext _db;

    public GetSessionResultQueryHandler(GradingDbContext db)
    {
        _db = db;
    }

    public async Task<SessionResultDto?> Handle(
        GetSessionResultQuery request,
        CancellationToken cancellationToken)
    {
        var session = await _db.TestSessions
            .AsNoTracking()
            .Include(s => s.TestAnswers)
                .ThenInclude(a => a.Question)
            .Include(s => s.TestAnswers)
                .ThenInclude(a => a.SelectedOptions)
            .Include(s => s.TestAnswers)
                .ThenInclude(a => a.AnswerParts)
                    .ThenInclude(ap => ap.QuestionPart)
            .FirstOrDefaultAsync(s => s.SessionId == request.SessionId, cancellationToken);

        // 404
        if (session is null)
            return null;

        // 403 — student does not own the session (BR-UC55-01)
        if (session.StudentId != request.AuthenticatedStudentId)
            throw new UnauthorizedAccessException(
                $"Student {request.AuthenticatedStudentId} does not own session {request.SessionId}.");

        var answers = session.TestAnswers
            .OrderBy(a => a.QuestionNo)
            .Select(a => new GradedAnswerDetailDto
            {
                QuestionId = a.QuestionId,
                QuestionNo = a.QuestionNo,
                QuestionType = a.Question.QuestionType,
                QuestionContent = a.Question.QuestionContent,
                DifficultyLevel = a.Question.DifficultyLevel,
                IsCorrect = a.IsCorrect,               // null when InProgress (BR-UC55-03)
                PointsEarned = a.PointsEarned,
                MaxPoints = a.Question.DefaultPoint,
                TimeSpent = a.TimeSpent,
                SelectedOptionId = a.AnswerId,
                ShortAnswerText = a.ShortAnswerText,
                SelectedOptionIds = a.SelectedOptions
                    .Select(o => o.AnswerId)
                    .ToList(),
                AnswerParts = a.AnswerParts
                    .Select(ap => new AnswerPartDetailDto
                    {
                        QuestionPartId = ap.QuestionPartId,
                        PartType = ap.QuestionPart.PartType,
                        StudentAnswer = ap.StudentAnswer,
                        IsCorrect = ap.IsCorrect,
                        PointsEarned = ap.PointsEarned,
                    })
                    .ToList(),
            })
            .ToList();

        return new SessionResultDto
        {
            SessionId = session.SessionId,
            TestId = session.TestId,
            TestFormat = session.TestFormat,
            Status = session.Status,
            Score = session.Score,
            NumCorrect = session.NumCorrect,
            NumIncorrect = session.NumIncorrect,
            NumAbandoned = session.NumAbandoned,
            TotalQuestion = session.TotalQuestion,
            DurationMinutes = session.Duration,
            SubmittedAt = session.EndTime,
            Answers = answers,
        };
    }
}
