using System.Text.Json;
using MathInsight.Modules.TestGen.Persistence;
using MathInsight.Modules.TestGen.Persistence.Entities;
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

    public async Task<IReadOnlyList<BlueprintExamCandidate>> GetCandidatesAsync(
        Blueprint blueprint,
        CancellationToken cancellationToken)
    {
        var sections = blueprint.Sections.ToList();
        var details = sections.SelectMany(section => section.Details).ToList();
        var difficultyIds = details
            .Select(detail => detail.DifficultyId)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        var questionTypes = sections
            .Select(section => section.QuestionType)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        var tagIds = details
            .Select(detail => detail.TagId)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var questions = await _context.Questions
            .AsNoTracking()
            .Where(question =>
                question.Grade == blueprint.Grade &&
                question.Status == ApprovedQuestionStatus &&
                question.IsActive &&
                difficultyIds.Contains(question.DifficultyId) &&
                questionTypes.Contains(question.QuestionType))
            .Select(question => new
            {
                question.QuestionId,
                question.DefaultWeight,
                question.DifficultyId,
                question.QuestionType
            })
            .ToListAsync(cancellationToken);

        if (questions.Count == 0)
            return Array.Empty<BlueprintExamCandidate>();

        var questionIds = questions.Select(question => question.QuestionId).ToList();
        var versionRows = await _context.QuestionVersions
            .AsNoTracking()
            .Where(version => questionIds.Contains(version.QuestionId))
            .Select(version => new LatestVersionCandidate(
                version.VersionId,
                version.QuestionId,
                version.VersionNumber,
                version.SnapshotSchemaVersion,
                version.AnswersSnapshot))
            .ToListAsync(cancellationToken);
        var latestVersionByQuestion = versionRows
            .GroupBy(version => version.QuestionId, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                group => group.Key,
                group => group.OrderByDescending(version => version.VersionNumber).First(),
                StringComparer.OrdinalIgnoreCase);
        var topicRows = await _context.QuestionTopics
            .AsNoTracking()
            .Where(topic => questionIds.Contains(topic.QuestionId) && tagIds.Contains(topic.TagId))
            .Select(topic => new { topic.QuestionId, topic.TagId })
            .ToListAsync(cancellationToken);

        var topicsByQuestion = topicRows
            .GroupBy(topic => topic.QuestionId, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                group => group.Key,
                group => (IReadOnlySet<string>)group
                    .Select(topic => topic.TagId)
                    .ToHashSet(StringComparer.OrdinalIgnoreCase),
                StringComparer.OrdinalIgnoreCase);

        return questions
            .Where(question => topicsByQuestion.ContainsKey(question.QuestionId) &&
                               latestVersionByQuestion.ContainsKey(question.QuestionId))
            .Select(question => CreateCandidate(
                question.QuestionId,
                question.DefaultWeight,
                question.DifficultyId,
                question.QuestionType,
                topicsByQuestion[question.QuestionId],
                latestVersionByQuestion[question.QuestionId]))
            .Where(candidate => candidate is not null)
            .Cast<BlueprintExamCandidate>()
            .ToList();
    }

    private static BlueprintExamCandidate? CreateCandidate(
        string questionId,
        decimal defaultWeight,
        string difficultyId,
        string questionType,
        IReadOnlySet<string> tagIds,
        LatestVersionCandidate version)
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

        if (snapshot is null)
            return null;

        var rules = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (!string.Equals(questionType, "Composite", StringComparison.OrdinalIgnoreCase))
        {
            rules.Add(ScoringRules.AllOrNothing);
        }
        else if (snapshot.Parts.Count > 0 && snapshot.Parts.All(part => part.DefaultWeight > 0m))
        {
            rules.Add(ScoringRules.WeightedParts);
            if (snapshot.Parts.Count == 4 && snapshot.Parts.All(part =>
                    string.Equals(
                        part.PartType.Replace("_", string.Empty),
                        "TrueFalse",
                        StringComparison.OrdinalIgnoreCase)))
            {
                rules.Add(ScoringRules.TieredTrueFalse);
            }
        }

        return rules.Count == 0
            ? null
            : new BlueprintExamCandidate(
                questionId,
                version.VersionId,
                defaultWeight,
                difficultyId,
                questionType,
                tagIds,
                rules);
    }

    private sealed record LatestVersionCandidate(
        string VersionId,
        string QuestionId,
        int VersionNumber,
        short SnapshotSchemaVersion,
        string AnswersSnapshot);
}
