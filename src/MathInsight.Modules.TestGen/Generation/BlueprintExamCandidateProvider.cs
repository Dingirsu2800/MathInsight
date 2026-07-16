using MathInsight.Modules.TestGen.Persistence;
using MathInsight.Modules.TestGen.Persistence.Entities;
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
                question.DifficultyId,
                question.QuestionType
            })
            .ToListAsync(cancellationToken);

        if (questions.Count == 0)
            return Array.Empty<BlueprintExamCandidate>();

        var questionIds = questions.Select(question => question.QuestionId).ToList();
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
            .Where(question => topicsByQuestion.ContainsKey(question.QuestionId))
            .Select(question => new BlueprintExamCandidate(
                question.QuestionId,
                question.DifficultyId,
                question.QuestionType,
                topicsByQuestion[question.QuestionId]))
            .ToList();
    }
}
