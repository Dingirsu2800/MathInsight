using System.Text.Json;
using MediatR;
using Microsoft.EntityFrameworkCore;
using MathInsight.Modules.Grading_Analytics.Persistence;
using MathInsight.Modules.Grading_Analytics.Persistence.Entities;
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
                    .ThenInclude(q => q.Answers)
            .Include(s => s.TestAnswers)
                .ThenInclude(a => a.Question)
                    .ThenInclude(q => q.Parts)
            .Include(s => s.TestAnswers)
                .ThenInclude(a => a.Question)
                    .ThenInclude(q => q.QuestionTopics)
            .Include(s => s.TestAnswers)
                .ThenInclude(a => a.SelectedOptions)
            .Include(s => s.TestAnswers)
                .ThenInclude(a => a.AnswerParts)
                    .ThenInclude(ap => ap.QuestionPart)
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

        var tagTopics = await _db.TagTopics
            .AsNoTracking()
            .ToDictionaryAsync(tt => tt.TagId, tt => tt.TagName, StringComparer.OrdinalIgnoreCase, cancellationToken);

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
                    difficultyLevel = (byte)level;
                }

                var primaryTopic = a.Question.QuestionTopics.FirstOrDefault(qt => qt.IsPrimary)
                    ?? a.Question.QuestionTopics.FirstOrDefault();
                string tagId = primaryTopic?.TagId ?? string.Empty;
                string topicName = !string.IsNullOrEmpty(tagId) && tagTopics.TryGetValue(tagId, out var tName)
                    ? tName
                    : string.Empty;

                var answerOptions = a.Question.Answers
                    .Where(ans => !ans.IsArchived)
                    .Select(ans => new AnswerOptionDetailDto
                    {
                        AnswerId = ans.AnswerId,
                        AnswerContent = ans.AnswerContent,
                        IsCorrect = ans.IsCorrect,
                        WasSelected = (a.AnswerId == ans.AnswerId) || a.SelectedOptions.Any(so => so.AnswerId == ans.AnswerId)
                    })
                    .ToList();

                var answerParts = a.Question.Parts.Count > 0
                    ? a.Question.Parts
                        .OrderBy(qp => qp.PartOrder)
                        .Select(qp =>
                        {
                            var studentAnswerPart = a.AnswerParts
                                .FirstOrDefault(ap => string.Equals(ap.PartId, qp.QuestionPartId, StringComparison.OrdinalIgnoreCase));

                            return new AnswerPartDetailDto
                            {
                                QuestionPartId = qp.QuestionPartId,
                                PartOrder = qp.PartOrder,
                                PartLabel = qp.PartLabel,
                                PartContent = qp.Content,
                                PartType = qp.PartType,
                                StudentAnswer = studentAnswerPart?.BooleanAnswer?.ToString()
                                                ?? studentAnswerPart?.TextAnswer
                                                ?? studentAnswerPart?.NumericAnswer?.ToString(),
                                CorrectAnswer = qp.CorrectBoolean?.ToString()
                                                ?? qp.CorrectText
                                                ?? qp.CorrectNumeric?.ToString(),
                                IsCorrect = studentAnswerPart?.IsCorrect,
                                PointsEarned = studentAnswerPart?.PointsEarned ?? 0m,
                                DefaultWeight = qp.DefaultWeight,
                                Explanation = qp.Explanation,
                            };
                        })
                        .ToList()
                    : a.AnswerParts
                        .Select(ap => new AnswerPartDetailDto
                        {
                            QuestionPartId = ap.PartId,
                            PartOrder = ap.QuestionPart?.PartOrder ?? 0,
                            PartLabel = ap.QuestionPart?.PartLabel,
                            PartContent = ap.QuestionPart?.Content ?? string.Empty,
                            PartType = ap.QuestionPart?.PartType ?? string.Empty,
                            StudentAnswer = ap.BooleanAnswer?.ToString()
                                            ?? ap.TextAnswer
                                            ?? ap.NumericAnswer?.ToString(),
                            CorrectAnswer = ap.QuestionPart?.CorrectBoolean?.ToString()
                                            ?? ap.QuestionPart?.CorrectText
                                            ?? ap.QuestionPart?.CorrectNumeric?.ToString(),
                            IsCorrect = ap.IsCorrect,
                            PointsEarned = ap.PointsEarned,
                            DefaultWeight = ap.QuestionPart?.DefaultWeight ?? 1m,
                            Explanation = ap.QuestionPart?.Explanation,
                        })
                        .OrderBy(ap => ap.PartOrder)
                        .ToList();

                return new GradedAnswerDetailDto
                {
                    QuestionId = a.QuestionId,
                    QuestionNo = a.QuestionNo,
                    QuestionType = a.Question.QuestionType,
                    QuestionContent = a.Question.QuestionContent,
                    SolutionContent = a.Question.SolutionContent ?? string.Empty,
                    PictureUrl = a.Question.PictureUrl,
                    DifficultyId = a.Question.DifficultyId,
                    DifficultyLevel = difficultyLevel,
                    TagId = tagId,
                    TopicName = topicName,
                    IsCorrect = a.IsCorrect,               // null when InProgress (BR-UC55-03)
                    PointsEarned = a.PointsEarned,
                    MachinePointsEarned = a.PointsEarned,
                    EffectivePoints = (tq?.IsScoreInvalidated == true) ? maxPoints : a.PointsEarned,
                    MaxPoints = maxPoints,
                    TimeSpent = a.TimeSpent,
                    IsScoreInvalidated = tq?.IsScoreInvalidated ?? false,
                    InvalidatedByReportId = tq?.InvalidatedByReportId,
                    SelectedOptionId = a.AnswerId,
                    ShortAnswerText = a.ShortAnswerText,
                    SelectedOptionIds = a.SelectedOptions
                        .Select(o => o.AnswerId)
                        .ToList(),
                    AnswerOptions = answerOptions,
                    AnswerParts = answerParts,
                    TagWeights = BuildTagWeightDtos(a.Question.QuestionTopics, tagTopics),
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

    /// <summary>
    /// Mirrors GradingOrchestrator.BuildTagWeights (v4.3) but resolves TopicName
    /// and returns API-layer DTOs instead of event records.
    /// single-tag  → Weight = 1.0
    /// multi-tag   → primary Weight = 0.77, each secondary Weight = 0.23 / N
    /// </summary>
    private static List<TagWeightEntryDto> BuildTagWeightDtos(
        ICollection<QuestionTopic> questionTopics,
        Dictionary<string, string> tagTopics)
    {
        if (questionTopics.Count == 0)
            return [];

        if (questionTopics.Count == 1)
        {
            var qt = questionTopics.First();
            var name = tagTopics.GetValueOrDefault(qt.TagId, string.Empty);
            return [new TagWeightEntryDto
            {
                TagId = qt.TagId,
                TopicName = name,
                Weight = 1.0m,
                IsPrimary = qt.IsPrimary
            }];
        }

        const decimal primaryWeight = 0.77m;
        var primary = questionTopics.FirstOrDefault(qt => qt.IsPrimary);
        var secondaries = questionTopics.Where(qt => !qt.IsPrimary).ToList();
        decimal secondaryWeight = secondaries.Count > 0 ? (1.0m - primaryWeight) / secondaries.Count : 0m;

        var weights = new List<TagWeightEntryDto>();

        if (primary is not null)
        {
            weights.Add(new TagWeightEntryDto
            {
                TagId = primary.TagId,
                TopicName = tagTopics.GetValueOrDefault(primary.TagId, string.Empty),
                Weight = primaryWeight,
                IsPrimary = true
            });
        }

        foreach (var sec in secondaries)
        {
            weights.Add(new TagWeightEntryDto
            {
                TagId = sec.TagId,
                TopicName = tagTopics.GetValueOrDefault(sec.TagId, string.Empty),
                Weight = secondaryWeight,
                IsPrimary = false
            });
        }

        return weights;
    }
}
