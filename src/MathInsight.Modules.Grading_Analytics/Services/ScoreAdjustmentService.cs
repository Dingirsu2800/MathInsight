using System.Data;
using System.Text.Json;
using MathInsight.Modules.Grading_Analytics.Persistence;
using MathInsight.Modules.Grading_Analytics.Persistence.Entities;
using MathInsight.Shared.Events;
using MathInsight.Shared.Questions;
using MathInsight.Shared.Scoring;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace MathInsight.Modules.Grading_Analytics.Services;

public sealed class ScoreAdjustmentService : IScoreAdjustmentService
{
    private const decimal PrimaryTagWeight = 0.77m;

    private readonly GradingDbContext _db;
    private readonly IPublisher _publisher;

    public ScoreAdjustmentService(GradingDbContext db, IPublisher publisher)
    {
        _db = db;
        _publisher = publisher;
    }

    public async Task AdjustInvalidQuestionVersionAsync(
        string reportId,
        CancellationToken cancellationToken = default)
    {
        var strategy = _db.Database.CreateExecutionStrategy();

        await strategy.ExecuteAsync(async ct =>
        {
            await using IDbContextTransaction? transaction = _db.Database.IsRelational()
                ? await _db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct)
                : null;

            var report = await _db.QuestionReports
                .FirstOrDefaultAsync(item => item.ReportId == reportId, ct)
                ?? throw new InvalidOperationException($"Question report '{reportId}' was not found.");

            QuestionReportIncident? incident = null;
            if (!string.IsNullOrWhiteSpace(report.IncidentId))
            {
                incident = await _db.QuestionReportIncidents
                    .FirstOrDefaultAsync(item => item.IncidentId == report.IncidentId, ct)
                    ?? throw new InvalidOperationException($"Question report incident '{report.IncidentId}' was not found.");

                if (string.Equals(incident.AdjustmentStatus, "Completed", StringComparison.OrdinalIgnoreCase))
                {
                    if (report.ScoreAdjustedTime is null)
                    {
                        report.ScoreAdjustedTime = DateTime.UtcNow;
                        await _db.SaveChangesAsync(ct);
                    }

                    if (transaction is not null)
                        await transaction.CommitAsync(ct);
                    return;
                }
            }

            if (report.Status != "Resolved" ||
                report.ResolutionAction != "InvalidateAndAwardFull" ||
                string.IsNullOrWhiteSpace(report.QuestionVersionId) ||
                (incident is null && report.ReporterRole != "Student") ||
                (incident is not null &&
                 (incident.RequiresAdminReview ||
                  !string.Equals(incident.Status, "AdjustmentPending", StringComparison.OrdinalIgnoreCase) ||
                  !string.Equals(incident.ApprovedResolutionAction, "InvalidateAndAwardFull", StringComparison.OrdinalIgnoreCase))))
            {
                throw new InvalidOperationException(
                    $"Question report '{reportId}' is not eligible for score adjustment.");
            }

            if (report.ScoreAdjustedTime is not null)
            {
                if (transaction is not null)
                    await transaction.CommitAsync(ct);
                return;
            }

            var versionTestQuestions = await _db.TestQuestions
                .Where(item => item.QuestionVersionId == report.QuestionVersionId)
                .ToListAsync(ct);

            var newlyInvalidated = versionTestQuestions
                .Where(item => !item.IsScoreInvalidated)
                .ToList();

            foreach (var testQuestion in newlyInvalidated)
            {
                testQuestion.IsScoreInvalidated = true;
                testQuestion.InvalidatedByReportId = report.ReportId;
            }

            var affectedTestIds = versionTestQuestions
                .Select(item => item.TestId)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
            var testsNeedingRevision = newlyInvalidated
                .Select(item => item.TestId)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            if (affectedTestIds.Count > 0)
            {
                var sessions = await _db.TestSessions
                    .Where(session => affectedTestIds.Contains(session.TestId) && session.Status == "Graded")
                    .Include(session => session.TestAnswers)
                        .ThenInclude(answer => answer.Question)
                            .ThenInclude(question => question.QuestionTopics)
                    .Include(session => session.TestAnswers)
                        .ThenInclude(answer => answer.SelectedOptions)
                    .Include(session => session.TestAnswers)
                        .ThenInclude(answer => answer.AnswerParts)
                    .ToListAsync(ct);

                var allTestQuestions = await _db.TestQuestions
                    .Where(item => affectedTestIds.Contains(item.TestId))
                    .Include(item => item.QuestionVersion)
                    .ToListAsync(ct);

                var difficultyLevels = await _db.TagDifficulties
                    .AsNoTracking()
                    .ToDictionaryAsync(td => td.DifficultyId, td => td.LevelValue, StringComparer.OrdinalIgnoreCase, ct);

                foreach (var session in sessions)
                {
                    var byQuestion = allTestQuestions
                        .Where(item => item.TestId == session.TestId)
                        .ToDictionary(item => item.QuestionId, StringComparer.OrdinalIgnoreCase);

                    if (testsNeedingRevision.Contains(session.TestId))
                    {
                        RecalculateSession(session, byQuestion);
                        session.GradeRevision = Math.Max(1, session.GradeRevision + 1);
                    }

                    var eventPayload = BuildGradeEvent(
                        session, byQuestion, difficultyLevels, report.ReportId, report.IncidentId);
                    var workExists = await _db.ScoreAdjustmentWorks.AnyAsync(item =>
                        item.SessionId == session.SessionId &&
                        item.GradeRevision == session.GradeRevision &&
                        (!string.IsNullOrWhiteSpace(report.IncidentId)
                            ? item.IncidentId == report.IncidentId
                            : item.IncidentId == null && item.ReportId == report.ReportId), ct);
                    if (!workExists)
                    {
                        _db.ScoreAdjustmentWorks.Add(new ScoreAdjustmentWork
                        {
                            WorkId = Guid.NewGuid().ToString(),
                            ReportId = report.ReportId,
                            IncidentId = report.IncidentId,
                            SessionId = session.SessionId,
                            GradeRevision = session.GradeRevision,
                            EventPayload = JsonSerializer.Serialize(eventPayload),
                            Status = "Pending",
                            AttemptCount = 0,
                            CreatedTime = DateTime.UtcNow
                        });
                    }
                }
            }

            await _db.SaveChangesAsync(ct);
            if (transaction is not null)
                await transaction.CommitAsync(ct);
        }, cancellationToken);

        await DispatchPendingAdjustmentsAsync(reportId, cancellationToken);
    }

    public async Task DispatchPendingAdjustmentsAsync(
        string? reportId = null,
        CancellationToken cancellationToken = default)
    {
        var deliveredReportIds = new HashSet<string>(StringComparer.Ordinal);
        while (true)
        {
            var work = await _db.ScoreAdjustmentWorks
                .Where(item => item.Status == "Pending" &&
                    (reportId == null || item.ReportId == reportId))
                .OrderBy(item => item.CreatedTime)
                .FirstOrDefaultAsync(cancellationToken);
            if (work is null)
                break;

            GradeCalculatedEvent? gradeEvent;
            try
            {
                gradeEvent = JsonSerializer.Deserialize<GradeCalculatedEvent>(work.EventPayload);
                if (gradeEvent is null)
                    throw new InvalidOperationException($"Score adjustment work '{work.WorkId}' has no valid event payload.");

                await _publisher.Publish(gradeEvent, cancellationToken);

                work.Status = "Completed";
                work.CompletedTime = DateTime.UtcNow;
                await _db.SaveChangesAsync(cancellationToken);
                deliveredReportIds.Add(work.ReportId);
            }
            catch (Exception exception) when (exception is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
            {
                work.AttemptCount++;
                work.LastError = exception.Message.Length <= 2000
                    ? exception.Message
                    : exception.Message[..2000];
                await _db.SaveChangesAsync(cancellationToken);
                if (!string.IsNullOrWhiteSpace(work.IncidentId))
                {
                    var incident = await _db.QuestionReportIncidents
                        .FirstOrDefaultAsync(item => item.IncidentId == work.IncidentId, cancellationToken);
                    if (incident is not null && incident.AdjustmentStatus != "Completed")
                    {
                        incident.AdjustmentStatus = "Failed";
                        await _db.SaveChangesAsync(cancellationToken);
                    }
                }
                throw;
            }
        }

        if (reportId is not null)
            deliveredReportIds.Add(reportId);

        foreach (var deliveredReportId in deliveredReportIds)
            await CompleteReportIfDeliveredAsync(deliveredReportId, cancellationToken);
    }

    public async Task RecoverPendingAdjustmentsAsync(CancellationToken cancellationToken = default)
    {
        // An approval can commit immediately before a process stops. Rebuild durable work from
        // the persisted incident state rather than depending on a one-time in-memory publish.
        var reportIds = await (
                from report in _db.QuestionReports
                join incident in _db.QuestionReportIncidents on report.IncidentId equals incident.IncidentId
                where report.Status == "Resolved" &&
                      report.ResolutionAction == "InvalidateAndAwardFull" &&
                      report.ScoreAdjustedTime == null &&
                      incident.Status == "AdjustmentPending" &&
                      !incident.RequiresAdminReview &&
                      incident.ApprovedResolutionAction == "InvalidateAndAwardFull" &&
                      incident.AdjustmentStatus != "Completed"
                orderby report.ReportId
                select new { report.ReportId, report.IncidentId })
            .ToListAsync(cancellationToken);

        foreach (var reportId in reportIds
                     .GroupBy(item => item.IncidentId, StringComparer.Ordinal)
                     .Select(group => group.First().ReportId))
        {
            await AdjustInvalidQuestionVersionAsync(reportId, cancellationToken);
        }
    }

    private async Task CompleteReportIfDeliveredAsync(string reportId, CancellationToken cancellationToken)
    {
        var hasPendingWork = await _db.ScoreAdjustmentWorks.AnyAsync(
            item => item.ReportId == reportId && item.Status == "Pending",
            cancellationToken);
        if (hasPendingWork)
            return;

        var adjustedReport = await _db.QuestionReports
            .FirstAsync(item => item.ReportId == reportId, cancellationToken);
        if (adjustedReport.ScoreAdjustedTime is not null)
            return;

        var now = DateTime.UtcNow;
        adjustedReport.ScoreAdjustedTime = now;
        if (!string.IsNullOrWhiteSpace(adjustedReport.IncidentId))
        {
            var incident = await _db.QuestionReportIncidents
                .FirstAsync(item => item.IncidentId == adjustedReport.IncidentId, cancellationToken);
            incident.AdjustmentStatus = "Completed";

            var relatedReports = await _db.QuestionReports
                .Where(item => item.IncidentId == incident.IncidentId && item.ScoreAdjustedTime == null)
                .ToListAsync(cancellationToken);
            foreach (var relatedReport in relatedReports)
                relatedReport.ScoreAdjustedTime = now;
        }
        await _db.SaveChangesAsync(cancellationToken);
    }

    private static void RecalculateSession(
        TestSession session,
        IReadOnlyDictionary<string, TestQuestion> testQuestions)
    {
        var earned = 0m;
        var max = 0m;
        var correct = 0;
        var incorrect = 0;
        var abandoned = 0;

        foreach (var answer in session.TestAnswers)
        {
            if (!testQuestions.TryGetValue(answer.QuestionId, out var testQuestion))
                throw new InvalidOperationException($"Missing TestQuestion for '{answer.QuestionId}'.");

            max += testQuestion.MaxPointsSnapshot;
            earned += testQuestion.IsScoreInvalidated
                ? testQuestion.MaxPointsSnapshot
                : answer.PointsEarned;

            if (testQuestion.IsScoreInvalidated)
                continue;

            var snapshot = DeserializeSnapshot(testQuestion);
            if (IsAbandoned(answer, snapshot.QuestionType))
            {
                abandoned++;
                continue;
            }

            if (answer.IsCorrect == true)
                correct++;
            else
                incorrect++;
        }

        session.Score = max > 0m ? Math.Round(earned / max * 10m, 2) : 0m;
        session.NumCorrect = correct;
        session.NumIncorrect = incorrect;
        session.NumAbandoned = abandoned;
    }

    private static GradeCalculatedEvent BuildGradeEvent(
        TestSession session,
        IReadOnlyDictionary<string, TestQuestion> testQuestions,
        IReadOnlyDictionary<string, int> difficultyLevels,
        string reportId,
        string? incidentId)
    {
        var answers = new List<GradedAnswerDto>();
        var tagStats = new Dictionary<string, (decimal Correct, decimal Total, decimal Earned, decimal Max)>(
            StringComparer.OrdinalIgnoreCase);

        foreach (var answer in session.TestAnswers)
        {
            var testQuestion = testQuestions[answer.QuestionId];
            var snapshot = DeserializeSnapshot(testQuestion);
            var tagWeights = BuildTagWeights(snapshot);
            var tagId = tagWeights.FirstOrDefault(item => item.IsPrimary)?.TagId
                ?? tagWeights.FirstOrDefault()?.TagId
                ?? string.Empty;
            var invalidated = testQuestion.IsScoreInvalidated;
            var earned = invalidated ? testQuestion.MaxPointsSnapshot : answer.PointsEarned;
            var isAbandoned = !invalidated && IsAbandoned(answer, snapshot.QuestionType);

            byte difficultyLevel = 1;
            if (!string.IsNullOrEmpty(answer.Question?.DifficultyId) &&
                difficultyLevels.TryGetValue(answer.Question.DifficultyId, out var level))
            {
                difficultyLevel = (byte)level;
            }

            answers.Add(new GradedAnswerDto
            {
                QuestionId = answer.QuestionId,
                TagId = tagId,
                TagWeights = tagWeights,
                NormalizedScore = testQuestion.MaxPointsSnapshot > 0m
                    ? Math.Round(earned / testQuestion.MaxPointsSnapshot * 10m, 2)
                    : 0m,
                IsCorrect = invalidated || answer.IsCorrect == true,
                MachineIsCorrect = answer.IsCorrect,
                IsScoreInvalidated = invalidated,
                PointsEarned = earned,
                MaxPoints = testQuestion.MaxPointsSnapshot,
                TimeSpent = answer.TimeSpent ?? 0,
                DifficultyLevel = difficultyLevel,
                QuestionNo = answer.QuestionNo,
                IsAbandoned = isAbandoned
            });

            foreach (var tagWeight in tagWeights)
            {
                tagStats.TryGetValue(tagWeight.TagId, out var stats);
                if (!invalidated)
                {
                    stats.Total++;
                    if (answer.IsCorrect == true)
                        stats.Correct++;
                    stats.Earned += answer.PointsEarned * tagWeight.Weight;
                    stats.Max += testQuestion.MaxPointsSnapshot * tagWeight.Weight;
                }
                tagStats[tagWeight.TagId] = stats;
            }
        }

        return new GradeCalculatedEvent
        {
            SessionId = session.SessionId,
            StudentId = session.StudentId,
            TestId = session.TestId,
            GradeRevision = session.GradeRevision,
            Cause = GradeCalculatedEvent.ScoreAdjustmentCause,
            ReportId = reportId,
            IncidentId = incidentId,
            TestFormat = session.TestFormat,
            Score = session.Score,
            NumCorrect = session.NumCorrect,
            NumIncorrect = session.NumIncorrect,
            NumAbandoned = session.NumAbandoned,
            Answers = answers,
            PerTagResults = tagStats.Select(item => new TopicGradeResult
            {
                TagId = item.Key,
                TotalItems = item.Value.Total,
                CorrectItems = item.Value.Correct,
                EarnedPoints = item.Value.Earned,
                MaxPoints = item.Value.Max,
                TopicScore = item.Value.Max > 0m
                    ? Math.Round(item.Value.Earned / item.Value.Max * 10m, 2)
                    : 0m
            }).ToList(),
            GradedAt = DateTime.UtcNow
        };
    }

    private static IReadOnlyList<TagWeightEntry> BuildTagWeights(QuestionSnapshotV2 snapshot)
    {
        var topics = snapshot.Topics.ToList();
        if (topics.Count == 0)
            return [];
        if (topics.Count == 1)
        {
            return [new TagWeightEntry
            {
                TagId = topics[0].TagId,
                Weight = 1m,
                IsPrimary = true
            }];
        }

        var primaryIndex = topics.FindIndex(item => item.IsPrimary);
        if (primaryIndex < 0)
            primaryIndex = 0;
        var secondaryWeight = (1m - PrimaryTagWeight) / (topics.Count - 1);

        return topics.Select((topic, index) => new TagWeightEntry
        {
            TagId = topic.TagId,
            Weight = index == primaryIndex ? PrimaryTagWeight : secondaryWeight,
            IsPrimary = index == primaryIndex
        }).ToList();
    }

    private static QuestionSnapshotV2 DeserializeSnapshot(TestQuestion testQuestion)
    {
        if (testQuestion.QuestionVersion is null || testQuestion.QuestionVersion.SnapshotSchemaVersion != 2)
            throw new InvalidOperationException(
                $"Unsupported snapshot schema for version '{testQuestion.QuestionVersionId}'.");

        return JsonSerializer.Deserialize<QuestionSnapshotV2>(testQuestion.QuestionVersion.AnswersSnapshot)
            ?? throw new InvalidOperationException(
                $"Invalid snapshot JSON for version '{testQuestion.QuestionVersionId}'.");
    }

    private static bool IsAbandoned(TestAnswer answer, string questionType)
    {
        var normalized = questionType.Replace("_", string.Empty).Replace(" ", string.Empty).ToUpperInvariant();
        return normalized switch
        {
            "SINGLECHOICE" or "TRUEFALSE" => answer.AnswerId is null,
            "MULTIPLESELECT" or "MULTIPLECHOICE" => answer.SelectedOptions.Count == 0,
            "SHORTANSWER" => string.IsNullOrWhiteSpace(answer.ShortAnswerText),
            "COMPOSITE" => answer.AnswerParts.Count == 0 || answer.AnswerParts.All(part =>
                part.BooleanAnswer is null && string.IsNullOrWhiteSpace(part.TextAnswer) && part.NumericAnswer is null),
            _ => true
        };
    }
}
