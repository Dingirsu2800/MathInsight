using System.Text.Json;
using MediatR;
using Microsoft.EntityFrameworkCore;
using MathInsight.Modules.Grading_Analytics.Persistence;
using MathInsight.Shared.Questions;

namespace MathInsight.Modules.Grading_Analytics.Queries.GetSessionResult;

/// <summary>
/// Returns the immutable question version that the student actually answered.
/// Current Question/Answer/QuestionPart rows are never used as answer truth here.
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

        var testQuestionRows = await _db.TestQuestions
            .AsNoTracking()
            .Where(item => item.TestId == session.TestId)
            .Include(item => item.QuestionVersion)
            .ToListAsync(cancellationToken);

        var testQuestions = testQuestionRows.ToDictionary(
            item => item.QuestionId,
            StringComparer.OrdinalIgnoreCase);

        var reportIds = testQuestionRows
            .Select(item => item.InvalidatedByReportId)
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Cast<string>()
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        var reports = reportIds.Count == 0
            ? new Dictionary<string, Persistence.Entities.QuestionReport>(StringComparer.OrdinalIgnoreCase)
            : (await _db.QuestionReports.AsNoTracking()
                .Where(item => reportIds.Contains(item.ReportId))
                .ToListAsync(cancellationToken))
                .ToDictionary(item => item.ReportId, StringComparer.OrdinalIgnoreCase);

        var answers = new List<GradedAnswerDetailDto>(session.TestAnswers.Count);
        foreach (var answer in session.TestAnswers.OrderBy(item => item.QuestionNo))
        {
            if (!testQuestions.TryGetValue(answer.QuestionId, out var testQuestion))
                throw new InvalidOperationException($"Missing TestQuestion snapshot for question '{answer.QuestionId}'.");

            if (testQuestion.QuestionVersion.SnapshotSchemaVersion != 2)
                throw new InvalidOperationException(
                    $"Unsupported snapshot schema for version '{testQuestion.QuestionVersionId}'.");

            var snapshot = JsonSerializer.Deserialize<QuestionSnapshotV2>(
                testQuestion.QuestionVersion.AnswersSnapshot)
                ?? throw new InvalidOperationException(
                    $"Invalid snapshot JSON for version '{testQuestion.QuestionVersionId}'.");

            var selectedOptionIds = answer.SelectedOptions
                .Select(item => item.AnswerId)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            if (!string.IsNullOrWhiteSpace(answer.AnswerId))
                selectedOptionIds.Add(answer.AnswerId);

            var submittedParts = answer.AnswerParts.ToDictionary(
                item => item.PartId,
                StringComparer.OrdinalIgnoreCase);

            var machinePoints = answer.PointsEarned;
            var effectivePoints = testQuestion.IsScoreInvalidated
                ? testQuestion.MaxPointsSnapshot
                : machinePoints;
            reports.TryGetValue(testQuestion.InvalidatedByReportId ?? string.Empty, out var invalidationReport);

            answers.Add(new GradedAnswerDetailDto
            {
                QuestionId = answer.QuestionId,
                QuestionVersionId = testQuestion.QuestionVersionId,
                QuestionNo = answer.QuestionNo,
                QuestionType = snapshot.QuestionType,
                QuestionContent = snapshot.QuestionContent ?? testQuestion.QuestionVersion.QuestionContent,
                PictureUrl = snapshot.PictureUrl ?? testQuestion.QuestionVersion.PictureUrl,
                SolutionContent = snapshot.SolutionContent ?? testQuestion.QuestionVersion.QuestionAnswer,
                DifficultyId = snapshot.DifficultyId,
                IsCorrect = answer.IsCorrect,
                PointsEarned = effectivePoints,
                MachinePointsEarned = machinePoints,
                EffectivePoints = effectivePoints,
                MaxPoints = testQuestion.MaxPointsSnapshot,
                TimeSpent = answer.TimeSpent,
                SelectedOptionId = answer.AnswerId,
                ShortAnswerText = answer.ShortAnswerText,
                SelectedOptionIds = answer.SelectedOptions.Select(item => item.AnswerId).ToList(),
                AnswerOptions = snapshot.Answers
                    .Select(option => new AnswerOptionDetailDto
                    {
                        AnswerId = option.AnswerId,
                        AnswerContent = option.AnswerContent,
                        IsCorrect = option.IsCorrect,
                        WasSelected = selectedOptionIds.Contains(option.AnswerId)
                    })
                    .ToList(),
                AnswerParts = snapshot.Parts
                    .OrderBy(part => part.PartOrder)
                    .Select(part =>
                    {
                        submittedParts.TryGetValue(part.PartId, out var submittedPart);
                        return new AnswerPartDetailDto
                        {
                            QuestionPartId = part.PartId,
                            PartOrder = part.PartOrder,
                            PartLabel = part.PartLabel,
                            PartContent = part.PartContent,
                            PartType = part.PartType,
                            StudentAnswer = submittedPart?.BooleanAnswer?.ToString()
                                ?? submittedPart?.TextAnswer
                                ?? submittedPart?.NumericAnswer?.ToString(),
                            CorrectAnswer = part.CorrectBoolean?.ToString()
                                ?? part.CorrectText
                                ?? part.CorrectNumeric?.ToString(),
                            IsCorrect = submittedPart?.IsCorrect,
                            PointsEarned = submittedPart?.PointsEarned ?? 0m,
                            DefaultWeight = part.DefaultWeight
                        };
                    })
                    .ToList(),
                IsScoreInvalidated = testQuestion.IsScoreInvalidated,
                ReportReason = invalidationReport?.ReportReason,
                ScoreAdjustedTime = invalidationReport?.ScoreAdjustedTime
            });
        }

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
            Answers = answers
        };
    }
}
