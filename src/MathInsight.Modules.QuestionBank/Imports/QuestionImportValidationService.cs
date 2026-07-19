using System.Globalization;
using System.Text;
using MathInsight.Modules.QuestionBank.Contracts.Imports;
using MathInsight.Modules.QuestionBank.Contracts.Questions;
using MathInsight.Modules.QuestionBank.Errors;
using MathInsight.Modules.QuestionBank.Persistence;
using MathInsight.Modules.QuestionBank.Validation;
using Microsoft.EntityFrameworkCore;

namespace MathInsight.Modules.QuestionBank.Imports;

public sealed class QuestionImportValidationService
{
    private readonly QuestionBankDbContext _context;

    public QuestionImportValidationService(QuestionBankDbContext context)
    {
        _context = context;
    }

    public async Task<QuestionImportPreviewResponse> BuildPreviewAsync(
        ParsedQuestionImportWorkbook workbook,
        string fileName,
        CancellationToken cancellationToken)
    {
        var activeTopics = await _context.TagTopics
            .AsNoTracking()
            .Where(topic => topic.IsActive)
            .ToListAsync(cancellationToken);
        var activeDifficulties = await _context.TagDifficulties
            .AsNoTracking()
            .Where(difficulty => difficulty.IsActive)
            .ToListAsync(cancellationToken);

        var fileErrors = workbook.ParserIssues
            .Where(issue => string.IsNullOrWhiteSpace(issue.QuestionKey))
            .ToList();
        var parserErrorsByKey = workbook.ParserIssues
            .Where(issue => !string.IsNullOrWhiteSpace(issue.QuestionKey))
            .GroupBy(issue => NormalizeKey(issue.QuestionKey!), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.ToList(), StringComparer.OrdinalIgnoreCase);

        var questionRowsByKey = workbook.Questions
            .GroupBy(row => NormalizeKey(row.QuestionKey), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.ToList(), StringComparer.OrdinalIgnoreCase);

        foreach (var parserIssue in workbook.ParserIssues.Where(issue =>
                     !string.IsNullOrWhiteSpace(issue.QuestionKey) &&
                     !questionRowsByKey.ContainsKey(NormalizeKey(issue.QuestionKey!))))
        {
            fileErrors.Add(parserIssue);
        }
        var answerRowsByKey = workbook.Answers
            .GroupBy(row => NormalizeKey(row.QuestionKey), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.OrderBy(item => item.SourceRow).ToList(), StringComparer.OrdinalIgnoreCase);
        var partRowsByKey = workbook.Parts
            .GroupBy(row => NormalizeKey(row.QuestionKey), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.OrderBy(item => item.SourceRow).ToList(), StringComparer.OrdinalIgnoreCase);
        var topicRowsByKey = workbook.Topics
            .GroupBy(row => NormalizeKey(row.QuestionKey), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.OrderBy(item => item.SourceRow).ToList(), StringComparer.OrdinalIgnoreCase);

        foreach (var key in answerRowsByKey.Keys.Concat(partRowsByKey.Keys).Concat(topicRowsByKey.Keys).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            if (questionRowsByKey.ContainsKey(key))
                continue;

            fileErrors.Add(Issue(
                QuestionBankErrors.QuestionImportOrphanRow.Code,
                QuestionBankErrors.QuestionImportOrphanRow.Message,
                "Questions",
                null,
                "QuestionKey",
                key));
        }

        if (workbook.Questions.Count == 0)
        {
            fileErrors.Add(Issue(
                QuestionBankErrors.QuestionImportNoQuestions.Code,
                QuestionBankErrors.QuestionImportNoQuestions.Message,
                "Questions",
                null,
                null,
                null));
        }

        var topicsByGradeAndName = activeTopics
            .GroupBy(topic => TopicLookupKey(topic.Grade, topic.TagName))
            .ToDictionary(group => group.Key, group => group.ToList(), StringComparer.Ordinal);
        var difficultyByLevel = activeDifficulties.ToDictionary(difficulty => difficulty.LevelValue);
        var items = new List<QuestionImportPreviewItemResponse>();

        foreach (var rawQuestion in workbook.Questions)
        {
            var normalizedKey = NormalizeKey(rawQuestion.QuestionKey);
            var errors = parserErrorsByKey.TryGetValue(normalizedKey, out var parserErrors)
                ? new List<QuestionImportIssueResponse>(parserErrors)
                : [];

            if (string.IsNullOrWhiteSpace(rawQuestion.QuestionKey))
                errors.Add(Issue(QuestionBankErrors.QuestionImportQuestionKeyInvalid.Code, QuestionBankErrors.QuestionImportQuestionKeyInvalid.Message, "Questions", rawQuestion.SourceRow, "QuestionKey", null));
            else if (rawQuestion.QuestionKey.Length > 50)
                errors.Add(Issue(QuestionBankErrors.QuestionImportQuestionKeyInvalid.Code, QuestionBankErrors.QuestionImportQuestionKeyInvalid.Message, "Questions", rawQuestion.SourceRow, "QuestionKey", rawQuestion.QuestionKey));
            else if (questionRowsByKey[normalizedKey].Count > 1)
                errors.Add(Issue(QuestionBankErrors.QuestionImportQuestionKeyDuplicate.Code, QuestionBankErrors.QuestionImportQuestionKeyDuplicate.Message, "Questions", rawQuestion.SourceRow, "QuestionKey", rawQuestion.QuestionKey));

            var request = BuildRequest(
                rawQuestion,
                answerRowsByKey.GetValueOrDefault(normalizedKey, []),
                partRowsByKey.GetValueOrDefault(normalizedKey, []),
                topicRowsByKey.GetValueOrDefault(normalizedKey, []),
                topicsByGradeAndName,
                difficultyByLevel,
                errors);

            var structuralError = QuestionRequestValidator.Validate(request, out _);
            if (structuralError is not null)
                errors.Add(Issue(structuralError.Code, structuralError.Message, "Questions", rawQuestion.SourceRow, null, rawQuestion.QuestionKey));

            items.Add(new QuestionImportPreviewItemResponse(
                rawQuestion.QuestionKey,
                rawQuestion.SourceRow,
                errors.Count == 0,
                errors,
                errors.Count == 0 ? request : null));
        }

        return new QuestionImportPreviewResponse(
            Guid.NewGuid().ToString(),
            fileName,
            items.Count,
            items.Count(item => item.IsValid),
            items.Count(item => !item.IsValid),
            fileErrors,
            items);
    }

    internal async Task<IReadOnlyList<QuestionImportIssueResponse>> ValidateConfirmAsync(
        IReadOnlyList<QuestionImportCandidate> candidates,
        CancellationToken cancellationToken)
    {
        var issues = new List<QuestionImportIssueResponse>();
        var topicIds = candidates.SelectMany(candidate => candidate.Draft.Topics).Select(topic => topic.TagId)
            .Where(id => !string.IsNullOrWhiteSpace(id)).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        var difficultyIds = candidates.Select(candidate => candidate.Draft.DifficultyId)
            .Where(id => !string.IsNullOrWhiteSpace(id)).Distinct(StringComparer.OrdinalIgnoreCase).ToList();

        var activeTopics = await _context.TagTopics
            .AsNoTracking()
            .Where(topic => topic.IsActive && topicIds.Contains(topic.TagId))
            .ToDictionaryAsync(topic => topic.TagId, StringComparer.OrdinalIgnoreCase, cancellationToken);
        var activeDifficulties = await _context.TagDifficulties
            .AsNoTracking()
            .Where(difficulty => difficulty.IsActive && difficultyIds.Contains(difficulty.DifficultyId))
            .Select(difficulty => difficulty.DifficultyId)
            .ToListAsync(cancellationToken);
        var activeDifficultyIds = activeDifficulties.ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var candidate in candidates)
        {
            var structuralError = QuestionRequestValidator.Validate(candidate.Draft, out _);
            if (structuralError is not null)
                issues.Add(Issue(structuralError.Code, structuralError.Message, "Confirm", null, null, candidate.QuestionKey));

            if (!activeDifficultyIds.Contains(candidate.Draft.DifficultyId))
                issues.Add(Issue(QuestionBankErrors.QuestionDifficultyNotFound.Code, "Difficulty is missing or inactive.", "Confirm", null, "DifficultyId", candidate.QuestionKey));

            foreach (var topic in candidate.Draft.Topics)
            {
                if (!activeTopics.TryGetValue(topic.TagId, out var resolvedTopic))
                {
                    issues.Add(Issue(QuestionBankErrors.QuestionTopicNotFound.Code, "Topic is missing or inactive.", "Confirm", null, "Topics", candidate.QuestionKey));
                    continue;
                }

                if (resolvedTopic.Grade != candidate.Draft.Grade)
                {
                    issues.Add(Issue(QuestionBankErrors.QuestionTopicNotFound.Code, "Topic grade must match question grade.", "Confirm", null, "Topics", candidate.QuestionKey));
                }
            }

            if (!string.IsNullOrWhiteSpace(candidate.Draft.PictureUrl) &&
                (!Uri.TryCreate(candidate.Draft.PictureUrl, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps || candidate.Draft.PictureUrl.Length > 255))
            {
                issues.Add(Issue(QuestionBankErrors.QuestionImportValidationFailed.Code, "PictureUrl must be an HTTPS URL with at most 255 characters.", "Confirm", null, "PictureUrl", candidate.QuestionKey));
            }
        }

        return issues;
    }

    private static CreateQuestionRequest BuildRequest(
        RawQuestionRow question,
        IReadOnlyList<RawAnswerRow> answerRows,
        IReadOnlyList<RawPartRow> partRows,
        IReadOnlyList<RawTopicRow> topicRows,
        IReadOnlyDictionary<string, List<Entities.TagTopic>> topicsByGradeAndName,
        IReadOnlyDictionary<int, Entities.TagDifficulty> difficultyByLevel,
        List<QuestionImportIssueResponse> errors)
    {
        var grade = ParseInt(question.Grade, "Questions", question.SourceRow, "Grade", question.QuestionKey, errors) ?? 0;
        var difficultyLevel = ParseInt(question.DifficultyLevel, "Questions", question.SourceRow, "DifficultyLevel", question.QuestionKey, errors);
        var defaultPoint = ParseDecimal(question.DefaultPoint, "Questions", question.SourceRow, "DefaultPoint", question.QuestionKey, errors) ?? 0.20m;
        if (!string.IsNullOrWhiteSpace(question.PictureUrl) &&
            (!Uri.TryCreate(question.PictureUrl, UriKind.Absolute, out var pictureUri) ||
             pictureUri.Scheme != Uri.UriSchemeHttps ||
             question.PictureUrl.Length > 255))
        {
            errors.Add(Issue(QuestionBankErrors.QuestionImportValidationFailed.Code, "PictureUrl must be an HTTPS URL with at most 255 characters.", "Questions", question.SourceRow, "PictureUrl", question.QuestionKey));
        }
        var difficultyId = string.Empty;
        if (difficultyLevel is not null)
        {
            if (!difficultyByLevel.TryGetValue(difficultyLevel.Value, out var difficulty))
                errors.Add(Issue(QuestionBankErrors.QuestionDifficultyNotFound.Code, "Difficulty is missing or inactive.", "Questions", question.SourceRow, "DifficultyLevel", question.QuestionKey));
            else
                difficultyId = difficulty.DifficultyId;
        }

        var topics = new List<CreateQuestionTopicRequest>();
        foreach (var topicRow in topicRows)
        {
            var isPrimary = ParseBoolean(topicRow.IsPrimary, "Topics", topicRow.SourceRow, "IsPrimary", question.QuestionKey, errors) ?? false;
            var topicLookupKey = TopicLookupKey(grade, topicRow.TopicName);
            if (!topicsByGradeAndName.TryGetValue(topicLookupKey, out var matchedTopics))
            {
                errors.Add(Issue(QuestionBankErrors.QuestionTopicNotFound.Code, "Topic is missing or inactive.", "Topics", topicRow.SourceRow, "TopicName", question.QuestionKey));
                continue;
            }

            if (matchedTopics.Count != 1)
            {
                errors.Add(Issue(
                    QuestionBankErrors.QuestionImportTopicAmbiguous.Code,
                    QuestionBankErrors.QuestionImportTopicAmbiguous.Message,
                    "Topics",
                    topicRow.SourceRow,
                    "TopicName",
                    question.QuestionKey));
                continue;
            }

            var topic = matchedTopics[0];
            topics.Add(new CreateQuestionTopicRequest(topic.TagId, isPrimary));
        }

        var answers = new List<CreateAnswerRequest>();
        foreach (var answerRow in answerRows)
        {
            var isCorrect = ParseBoolean(answerRow.IsCorrect, "Answers", answerRow.SourceRow, "IsCorrect", question.QuestionKey, errors) ?? false;
            answers.Add(new CreateAnswerRequest { AnswerContent = answerRow.AnswerContent, IsCorrect = isCorrect });
        }

        var parts = new List<CreateQuestionPartRequest>();
        foreach (var partRow in partRows)
        {
            var partOrder = ParseInt(partRow.PartOrder, "Parts", partRow.SourceRow, "PartOrder", question.QuestionKey, errors) ?? 0;
            var partDefaultPoint = ParseDecimal(partRow.DefaultPoint, "Parts", partRow.SourceRow, "DefaultPoint", question.QuestionKey, errors) ?? 0m;
            parts.Add(new CreateQuestionPartRequest
            {
                PartOrder = partOrder,
                PartLabel = EmptyToNull(partRow.PartLabel),
                PartContent = partRow.PartContent,
                PartType = partRow.PartType,
                CorrectBoolean = ParseBoolean(partRow.CorrectBoolean, "Parts", partRow.SourceRow, "CorrectBoolean", question.QuestionKey, errors),
                CorrectText = EmptyToNull(partRow.CorrectText),
                CorrectNumeric = ParseDecimal(partRow.CorrectNumeric, "Parts", partRow.SourceRow, "CorrectNumeric", question.QuestionKey, errors),
                NumericTolerance = ParseDecimal(partRow.NumericTolerance, "Parts", partRow.SourceRow, "NumericTolerance", question.QuestionKey, errors),
                Explanation = EmptyToNull(partRow.Explanation),
                DefaultPoint = partDefaultPoint
            });
        }

        return new CreateQuestionRequest
        {
            QuestionContent = question.QuestionContent,
            SolutionContent = question.SolutionContent,
            PictureUrl = EmptyToNull(question.PictureUrl),
            DifficultyId = difficultyId,
            Grade = grade,
            QuestionType = question.QuestionType,
            DefaultPoint = defaultPoint,
            Topics = topics,
            Answers = answers,
            Parts = parts
        };
    }

    private static int? ParseInt(string value, string sheet, int row, string column, string questionKey, List<QuestionImportIssueResponse> errors)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            errors.Add(Issue(QuestionBankErrors.QuestionImportNumericInvalid.Code, "A numeric value is required.", sheet, row, column, questionKey));
            return null;
        }

        if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed))
            return parsed;

        errors.Add(Issue(QuestionBankErrors.QuestionImportNumericInvalid.Code, "Value must be an integer.", sheet, row, column, questionKey));
        return null;
    }

    private static decimal? ParseDecimal(string value, string sheet, int row, string column, string questionKey, List<QuestionImportIssueResponse> errors)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        var normalized = value.Trim();
        if (normalized.Contains(',') && normalized.Contains('.'))
        {
            errors.Add(Issue(QuestionBankErrors.QuestionImportNumericInvalid.Code, QuestionBankErrors.QuestionImportNumericInvalid.Message, sheet, row, column, questionKey));
            return null;
        }

        normalized = normalized.Replace(',', '.');
        const NumberStyles styles = NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint;
        if (decimal.TryParse(normalized, styles, CultureInfo.InvariantCulture, out var parsed))
            return parsed;

        errors.Add(Issue(QuestionBankErrors.QuestionImportNumericInvalid.Code, QuestionBankErrors.QuestionImportNumericInvalid.Message, sheet, row, column, questionKey));
        return null;
    }

    private static bool? ParseBoolean(string value, string sheet, int row, string column, string questionKey, List<QuestionImportIssueResponse> errors)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        var normalized = value.Trim().ToUpperInvariant();
        if (normalized is "TRUE" or "1" or "YES" or "Y" or "ĐÚNG")
            return true;
        if (normalized is "FALSE" or "0" or "NO" or "N" or "SAI")
            return false;

        errors.Add(Issue(QuestionBankErrors.QuestionImportBooleanInvalid.Code, QuestionBankErrors.QuestionImportBooleanInvalid.Message, sheet, row, column, questionKey));
        return null;
    }

    private static string NormalizeKey(string key) => key.Trim();

    private static string TopicLookupKey(int grade, string topicName) =>
        $"{grade}|{NormalizeLookupText(topicName)}";

    private static string NormalizeLookupText(string value)
    {
        var normalized = value.Normalize(NormalizationForm.FormC).Trim();
        return string.Join(' ', normalized.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries))
            .ToUpperInvariant();
    }

    private static string? EmptyToNull(string value) => string.IsNullOrWhiteSpace(value) ? null : value;

    private static QuestionImportIssueResponse Issue(
        string code,
        string message,
        string sheet,
        int? row,
        string? column,
        string? questionKey) => new(code, message, sheet, row, column, questionKey);
}

internal sealed record QuestionImportCandidate(string QuestionKey, CreateQuestionRequest Draft);
