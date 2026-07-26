using System.Text.Json;
using MathInsight.Modules.TestGen.Persistence;
using MathInsight.Modules.TestGen.Persistence.Entities;
using MathInsight.Modules.TestGen.Persistence.ReadModels;
using MathInsight.Shared.Questions;
using MathInsight.Shared.Scoring;
using Microsoft.EntityFrameworkCore;

namespace MathInsight.Modules.TestGen.Generation;

public sealed class BlueprintExamCandidateProvider : IBlueprintExamCandidateProvider
{
    private const string ApprovedQuestionStatus = "Approved";

    private readonly TestGenDbContext _context;

    public BlueprintExamCandidateProvider(TestGenDbContext context)
    {
        _context = context;
    }

    public async Task<BlueprintExamCandidatePool> GetCandidatesAsync(
        Blueprint blueprint,
        CancellationToken cancellationToken)
    {
        var sections = blueprint.Sections.ToList();
        var details = sections.SelectMany(section => section.Details).ToList();
        var difficultyIds = details.Select(detail => detail.DifficultyId).Distinct().ToList();
        var questionTypes = sections.Select(section => section.QuestionType).Distinct().ToList();
        var tagIds = details.Select(detail => detail.TagId).Distinct().ToList();

        var questions = await _context.Questions
            .AsNoTracking()
            .Where(question =>
                question.Grade == blueprint.Grade &&
                question.Status == ApprovedQuestionStatus &&
                question.IsActive &&
                difficultyIds.Contains(question.DifficultyId) &&
                questionTypes.Contains(question.QuestionType) &&
                question.Topics.Any(topic => tagIds.Contains(topic.TagId)))
            .ToListAsync(cancellationToken);

        if (questions.Count == 0)
            return new BlueprintExamCandidatePool([], []);

        var questionIds = questions.Select(question => question.QuestionId).ToList();
        var versions = await _context.QuestionVersions
            .AsNoTracking()
            .Where(version => questionIds.Contains(version.QuestionId))
            .ToListAsync(cancellationToken);
        var latestVersions = versions
            .GroupBy(version => version.QuestionId, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                group => group.Key,
                group => group.OrderByDescending(version => version.VersionNumber).First(),
                StringComparer.OrdinalIgnoreCase);
        var topics = await _context.QuestionTopics
            .AsNoTracking()
            .Where(topic => questionIds.Contains(topic.QuestionId))
            .ToListAsync(cancellationToken);
        var answers = await _context.Answers
            .AsNoTracking()
            .Where(answer => questionIds.Contains(answer.QuestionId) && !answer.IsArchived)
            .ToListAsync(cancellationToken);
        var parts = await _context.QuestionParts
            .AsNoTracking()
            .Where(part => questionIds.Contains(part.QuestionId) && !part.IsArchived)
            .ToListAsync(cancellationToken);

        var topicsByQuestion = ToLookup(topics, topic => topic.QuestionId);
        var answersByQuestion = ToLookup(answers, answer => answer.QuestionId);
        var partsByQuestion = ToLookup(parts, part => part.QuestionId);
        var candidates = new List<BlueprintExamCandidate>();
        var invalidVersionCandidates = new List<BlueprintExamCandidate>();

        foreach (var question in questions)
        {
            if (!latestVersions.TryGetValue(question.QuestionId, out var version))
            {
                AddDiagnosticCandidate(
                    invalidVersionCandidates,
                    question,
                    GetValues(topicsByQuestion, question.QuestionId),
                    GetValues(answersByQuestion, question.QuestionId),
                    GetValues(partsByQuestion, question.QuestionId));
                continue;
            }

            var candidate = CreateCandidate(
                question,
                version,
                GetValues(topicsByQuestion, question.QuestionId),
                GetValues(answersByQuestion, question.QuestionId),
                GetValues(partsByQuestion, question.QuestionId));
            if (candidate is null)
            {
                AddDiagnosticCandidate(
                    invalidVersionCandidates,
                    question,
                    GetValues(topicsByQuestion, question.QuestionId),
                    GetValues(answersByQuestion, question.QuestionId),
                    GetValues(partsByQuestion, question.QuestionId));
                continue;
            }

            candidates.Add(candidate);
        }

        return new BlueprintExamCandidatePool(candidates, invalidVersionCandidates);
    }

    private static void AddDiagnosticCandidate(
        ICollection<BlueprintExamCandidate> candidates,
        QuestionReadModel question,
        IReadOnlyList<QuestionTopicReadModel> currentTopics,
        IReadOnlyList<AnswerReadModel> currentAnswers,
        IReadOnlyList<QuestionPartReadModel> currentParts)
    {
        if (currentTopics.Count == 0 || question.DefaultWeight <= 0m)
            return;

        var rules = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var partCount = 0;
        if (!string.Equals(question.QuestionType, "Composite", StringComparison.OrdinalIgnoreCase))
        {
            if (currentAnswers.Count == 0)
                return;
            rules.Add(ScoringRules.AllOrNothing);
        }
        else
        {
            if (currentParts.Count == 0 || currentParts.Any(part => part.DefaultWeight <= 0m))
                return;
            partCount = currentParts.Count;
            rules.Add(ScoringRules.WeightedParts);
            if (currentParts.Count == 4 && currentParts.All(part =>
                    string.Equals(NormalizeType(part.PartType), "TrueFalse", StringComparison.OrdinalIgnoreCase)))
            {
                rules.Add(ScoringRules.TieredTrueFalse);
            }
        }

        candidates.Add(new BlueprintExamCandidate(
            question.QuestionId,
            string.Empty,
            question.DefaultWeight,
            question.DifficultyId,
            question.QuestionType,
            currentTopics.Select(topic => topic.TagId).ToHashSet(StringComparer.OrdinalIgnoreCase),
            rules,
            partCount));
    }

    private static BlueprintExamCandidate? CreateCandidate(
        QuestionReadModel question,
        QuestionVersionReadModel version,
        IReadOnlyList<QuestionTopicReadModel> currentTopics,
        IReadOnlyList<AnswerReadModel> currentAnswers,
        IReadOnlyList<QuestionPartReadModel> currentParts)
    {
        if (version.SnapshotSchemaVersion != 2)
            return null;

        QuestionSnapshotV2? snapshot;
        try
        {
            snapshot = JsonSerializer.Deserialize<QuestionSnapshotV2>(version.AnswersSnapshot);
        }
        catch (JsonException)
        {
            return null;
        }

        if (snapshot is null ||
            snapshot.Topics is null ||
            snapshot.Answers is null ||
            snapshot.Parts is null ||
            !string.Equals(snapshot.QuestionId, question.QuestionId, StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(snapshot.QuestionType, question.QuestionType, StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(snapshot.DifficultyId, question.DifficultyId, StringComparison.OrdinalIgnoreCase) ||
            snapshot.Grade != question.Grade ||
            snapshot.DefaultWeight <= 0m ||
            snapshot.DefaultWeight != question.DefaultWeight ||
            string.IsNullOrWhiteSpace(snapshot.QuestionContent) ||
            string.IsNullOrWhiteSpace(snapshot.SolutionContent) ||
            !TopicsMatch(snapshot.Topics, currentTopics))
        {
            return null;
        }

        var rules = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (!string.Equals(question.QuestionType, "Composite", StringComparison.OrdinalIgnoreCase))
        {
            if (!AnswersMatch(question.QuestionType, snapshot.Answers, currentAnswers) ||
                snapshot.Parts.Count != 0 ||
                currentParts.Count != 0)
            {
                return null;
            }

            rules.Add(ScoringRules.AllOrNothing);
        }
        else
        {
            if (snapshot.Answers.Count != 0 ||
                currentAnswers.Count != 0 ||
                !PartsMatch(snapshot.Parts, currentParts))
            {
                return null;
            }

            rules.Add(ScoringRules.WeightedParts);
            if (snapshot.Parts.Count == 4 && snapshot.Parts.All(part =>
                    string.Equals(NormalizeType(part.PartType), "TrueFalse", StringComparison.OrdinalIgnoreCase)))
            {
                rules.Add(ScoringRules.TieredTrueFalse);
            }
        }

        return new BlueprintExamCandidate(
            question.QuestionId,
            version.VersionId,
            snapshot.DefaultWeight,
            question.DifficultyId,
            question.QuestionType,
            snapshot.Topics.Select(topic => topic.TagId).ToHashSet(StringComparer.OrdinalIgnoreCase),
            rules,
            snapshot.Parts.Count);
    }

    private static bool TopicsMatch(
        IReadOnlyList<QuestionTopicSnapshot> snapshotTopics,
        IReadOnlyList<QuestionTopicReadModel> currentTopics)
    {
        if (snapshotTopics.Count == 0 ||
            snapshotTopics.Any(topic => string.IsNullOrWhiteSpace(topic.TagId)) ||
            snapshotTopics.Select(topic => topic.TagId).Distinct(StringComparer.OrdinalIgnoreCase).Count() != snapshotTopics.Count)
        {
            return false;
        }

        var snapshotValues = snapshotTopics
            .Select(topic => $"{topic.TagId.ToUpperInvariant()}:{topic.IsPrimary}")
            .OrderBy(value => value)
            .ToList();
        var currentValues = currentTopics
            .Select(topic => $"{topic.TagId.ToUpperInvariant()}:{topic.IsPrimary}")
            .OrderBy(value => value)
            .ToList();
        return snapshotValues.SequenceEqual(currentValues);
    }

    private static bool AnswersMatch(
        string questionType,
        IReadOnlyList<QuestionAnswerSnapshot> snapshotAnswers,
        IReadOnlyList<AnswerReadModel> currentAnswers)
    {
        if (snapshotAnswers.Count == 0 ||
            snapshotAnswers.Any(answer => string.IsNullOrWhiteSpace(answer.AnswerId) || string.IsNullOrWhiteSpace(answer.AnswerContent)) ||
            snapshotAnswers.Select(answer => answer.AnswerId).Distinct(StringComparer.OrdinalIgnoreCase).Count() != snapshotAnswers.Count)
        {
            return false;
        }

        var correctCount = snapshotAnswers.Count(answer => answer.IsCorrect);
        var validCorrectShape = questionType switch
        {
            "SingleChoice" => correctCount == 1,
            "TrueFalse" => snapshotAnswers.Count == 2 && correctCount == 1,
            "MultipleChoice" => correctCount > 0,
            "ShortAnswer" => correctCount == 1,
            _ => false
        };
        if (!validCorrectShape || snapshotAnswers.Count != currentAnswers.Count)
            return false;

        var currentById = currentAnswers.ToDictionary(answer => answer.AnswerId, StringComparer.OrdinalIgnoreCase);
        return snapshotAnswers.All(answer =>
            currentById.TryGetValue(answer.AnswerId, out var current) && current.IsCorrect == answer.IsCorrect);
    }

    private static bool PartsMatch(
        IReadOnlyList<QuestionPartSnapshot> snapshotParts,
        IReadOnlyList<QuestionPartReadModel> currentParts)
    {
        if (snapshotParts.Count == 0 ||
            snapshotParts.Count != currentParts.Count ||
            snapshotParts.Any(part =>
                string.IsNullOrWhiteSpace(part.PartId) ||
                string.IsNullOrWhiteSpace(part.PartType) ||
                string.IsNullOrWhiteSpace(part.PartContent) ||
                part.PartOrder <= 0 ||
                part.DefaultWeight <= 0m ||
                !HasValidPartAnswer(part)) ||
            snapshotParts.Select(part => part.PartId).Distinct(StringComparer.OrdinalIgnoreCase).Count() != snapshotParts.Count ||
            snapshotParts.Select(part => part.PartOrder).Distinct().Count() != snapshotParts.Count)
        {
            return false;
        }

        var currentById = currentParts.ToDictionary(part => part.PartId, StringComparer.OrdinalIgnoreCase);
        return snapshotParts.All(part =>
            currentById.TryGetValue(part.PartId, out var current) &&
            current.PartOrder == part.PartOrder &&
            string.Equals(NormalizeType(current.PartType), NormalizeType(part.PartType), StringComparison.OrdinalIgnoreCase) &&
            current.DefaultWeight == part.DefaultWeight &&
            current.CorrectBoolean == part.CorrectBoolean &&
            current.CorrectText == part.CorrectText &&
            current.CorrectNumeric == part.CorrectNumeric &&
            current.NumericTolerance == part.NumericTolerance);
    }

    private static bool HasValidPartAnswer(QuestionPartSnapshot part)
        => NormalizeType(part.PartType) switch
        {
            "TrueFalse" => part.CorrectBoolean is not null && part.CorrectText is null && part.CorrectNumeric is null && part.NumericTolerance is null,
            "ShortAnswer" => part.CorrectBoolean is null && !string.IsNullOrWhiteSpace(part.CorrectText) && part.CorrectNumeric is null && part.NumericTolerance is null,
            "NumericAnswer" => part.CorrectBoolean is null && part.CorrectText is null && part.CorrectNumeric is not null && (part.NumericTolerance is null || part.NumericTolerance >= 0m),
            _ => false
        };

    private static string NormalizeType(string value) => value.Replace("_", string.Empty, StringComparison.Ordinal);

    private static Dictionary<string, List<T>> ToLookup<T>(
        IEnumerable<T> values,
        Func<T, string> keySelector)
        => values
            .GroupBy(keySelector, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.ToList(), StringComparer.OrdinalIgnoreCase);

    private static IReadOnlyList<T> GetValues<T>(Dictionary<string, List<T>> values, string key)
        => values.TryGetValue(key, out var result) ? result : [];
}
