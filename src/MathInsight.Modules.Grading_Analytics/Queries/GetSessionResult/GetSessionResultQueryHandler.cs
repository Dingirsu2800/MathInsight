using System.Text.Json;
using MediatR;
using Microsoft.EntityFrameworkCore;
using MathInsight.Modules.Grading_Analytics.Persistence;
using MathInsight.Shared.Questions;

namespace MathInsight.Modules.Grading_Analytics.Queries.GetSessionResult;

/// <summary>
/// Handles GetSessionResultQuery (UC-55).
/// Loads session + all nested navigation properties required for the result page.
/// Uses TestQuestion.MaxPointsSnapshot for MaxPoints and exposes invalidation info.
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
            .FirstOrDefaultAsync(s => s.SessionId == request.SessionId, cancellationToken);

        if (session is null)
            return null;

        if (!string.Equals(session.StudentId, request.AuthenticatedStudentId, StringComparison.Ordinal))
            throw new UnauthorizedAccessException(
                $"Student {request.AuthenticatedStudentId} does not own session {request.SessionId}.");

        // ── Load TestQuestion scoring snapshots for this test ─────────────────
        var testQuestions = await _db.TestQuestions
            .AsNoTracking()
            .Where(tq => tq.TestId == session.TestId)
            .ToDictionaryAsync(tq => tq.QuestionId, cancellationToken);

        var difficultyLevels = await _db.TagDifficulties
            .AsNoTracking()
            .ToDictionaryAsync(td => td.DifficultyId, td => td.LevelValue, StringComparer.OrdinalIgnoreCase, cancellationToken);

        var answers = session.TestAnswers
            .OrderBy(a => a.QuestionNo)
            .Select(a =>
            {
                var tq = testQuestions.GetValueOrDefault(a.QuestionId);
                decimal maxPoints = tq?.MaxPointsSnapshot ?? a.Question.DefaultWeight;

                byte difficultyLevel = 1;
                if (!string.IsNullOrEmpty(a.Question.DifficultyId) &&
                    difficultyLevels.TryGetValue(a.Question.DifficultyId, out var level))
                {
                    difficultyLevel = level;
                }

                return new GradedAnswerDetailDto
                {
                    QuestionId = a.QuestionId,
                    QuestionNo = a.QuestionNo,
                    QuestionType = a.Question.QuestionType,
                    QuestionContent = a.Question.QuestionContent,
                    DifficultyId = a.Question.DifficultyId,
                    DifficultyLevel = difficultyLevel,
                    IsCorrect = a.IsCorrect,               // null when InProgress (BR-UC55-03)
                    PointsEarned = a.PointsEarned,
                    MaxPoints = maxPoints,
                    TimeSpent = a.TimeSpent,
                    IsScoreInvalidated = tq?.IsScoreInvalidated ?? false,
                    InvalidatedByReportId = tq?.InvalidatedByReportId,
                    SelectedOptionId = a.AnswerId,
                    ShortAnswerText = a.ShortAnswerText,
                    SelectedOptionIds = a.SelectedOptions
                        .Select(o => o.AnswerId)
                        .ToList(),
                    AnswerParts = a.AnswerParts
                        .Select(ap => new AnswerPartDetailDto
                        {
                            QuestionPartId = ap.PartId,
                            PartType = ap.QuestionPart.PartType,
                            StudentAnswer = ap.BooleanAnswer?.ToString() 
                                            ?? ap.TextAnswer 
                                            ?? ap.NumericAnswer?.ToString(),
                            IsCorrect = ap.IsCorrect,
                            PointsEarned = ap.PointsEarned,
                        })
                        .ToList(),
                };
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
            GradeRevision = session.GradeRevision,
            Answers = answers,
        };
    }
}
