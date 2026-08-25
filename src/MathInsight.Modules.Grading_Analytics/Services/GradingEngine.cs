using MathInsight.Modules.Grading_Analytics.Persistence.Entities;
using MathInsight.Shared.Questions;
using MathInsight.Shared.Scoring;

namespace MathInsight.Modules.Grading_Analytics.Services;

/// <summary>
/// Per-question-type grading logic.
/// Grades all answers for a session synchronously by mutating IsCorrect and PointsEarned
/// on TestAnswer (and TestAnswerPart for COMPOSITE) entities in-place.
/// </summary>
public class GradingEngine : IGradingEngine
{
    private static readonly decimal[] CompositeAllTfScoreTable = [0.00m, 0.10m, 0.25m, 0.50m, 1.00m];

    public GradingResult Grade(TestSession session)
    {
        var correct = 0;
        var incorrect = 0;
        var abandoned = 0;
        var effectiveEarned = 0m;
        var totalMax = 0m;

        foreach (var answer in session.TestAnswers)
        {
            var question = answer.Question;
            var testQuestion = answer.TestQuestion;

            decimal maxPoints = testQuestion?.MaxPointsSnapshot ?? question.DefaultWeight;
            totalMax += maxPoints;

            // ── Score invalidation ─────────────────────────────────────────
            if (testQuestion?.IsScoreInvalidated == true)
            {
                answer.IsCorrect = null;
                answer.PointsEarned = maxPoints;
                effectiveEarned += maxPoints;
                continue;
            }

            bool isAbandoned = IsAbandoned(answer, question.QuestionType);

            if (isAbandoned)
            {
                answer.IsCorrect = false;
                answer.PointsEarned = 0m;
                abandoned++;
                continue;
            }

            // ── Determine grading strategy ─────────────────────────────────
            var scoringRule = testQuestion?.ScoringRuleSnapshot;

            if (!string.IsNullOrEmpty(scoringRule))
            {
                GradeByScoringRule(answer, question, maxPoints, scoringRule);
            }
            else
            {
                GradeByQuestionType(answer, question, maxPoints);
            }

            effectiveEarned += answer.PointsEarned;

            if (answer.IsCorrect == true)
            {
                correct++;
            }
            else
            {
                incorrect++;
            }
        }

        var score = totalMax > 0m
            ? Math.Round(effectiveEarned / totalMax * 10m, 2)
            : 0m;

        return new GradingResult
        {
            Score = Math.Clamp(score, 0m, 10m),
            NumCorrect = correct,
            NumIncorrect = incorrect,
            NumAbandoned = abandoned
        };
    }

    private static void GradeByScoringRule(TestAnswer answer, Question question, decimal maxPoints, string scoringRule)
    {
        var ruleNormalized = scoringRule.Replace("_", "").Replace(" ", "").ToUpperInvariant();

        switch (ruleNormalized)
        {
            case "ALLORNOTHING":
                GradeAllOrNothing(answer, question, maxPoints);
                break;

            case "TIEREDTRUEFALSE":
                GradeCompositeAllTrueFalse(answer, question, maxPoints);
                break;

            case "WEIGHTEDPARTS":
                GradeCompositeGeneral(answer, question, maxPoints);
                break;

            default:
                GradeByQuestionType(answer, question, maxPoints);
                break;
        }
    }

    private static void GradeByQuestionType(TestAnswer answer, Question question, decimal maxPoints)
    {
        var typeNormalized = NormalizeType(question.QuestionType);

        switch (typeNormalized)
        {
            case "SINGLECHOICE":
            case "TRUEFALSE":
                GradeSingleChoice(answer, question, maxPoints);
                break;

            case "MULTIPLESELECT":
            case "MULTIPLECHOICE":
                GradeMultipleSelect(answer, question, maxPoints);
                break;

            case "SHORTANSWER":
                GradeShortAnswer(answer, question, maxPoints);
                break;

            case "COMPOSITE":
                GradeComposite(answer, question, maxPoints);
                break;

            default:
                answer.IsCorrect = false;
                answer.PointsEarned = 0m;
                break;
        }
    }

    private static bool IsAbandoned(TestAnswer answer, string questionType)
    {
        var type = NormalizeType(questionType);
        return type switch
        {
            "SINGLECHOICE" or "TRUEFALSE" => answer.AnswerId is null,
            "MULTIPLESELECT" or "MULTIPLECHOICE" => answer.SelectedOptions.Count == 0,
            "SHORTANSWER" => string.IsNullOrWhiteSpace(answer.ShortAnswerText),
            "COMPOSITE" => answer.AnswerParts.Count == 0 || answer.AnswerParts.All(part =>
                part.BooleanAnswer is null && string.IsNullOrWhiteSpace(part.TextAnswer) && part.NumericAnswer is null),
            _ => true
        };
    }

    private static void GradeAllOrNothing(TestAnswer answer, Question question, decimal maxPoints)
    {
        var typeNormalized = NormalizeType(question.QuestionType);

        switch (typeNormalized)
        {
            case "SINGLECHOICE":
            case "TRUEFALSE":
                GradeSingleChoice(answer, question, maxPoints);
                break;

            case "MULTIPLESELECT":
            case "MULTIPLECHOICE":
                GradeMultipleSelect(answer, question, maxPoints);
                break;

            case "SHORTANSWER":
                GradeShortAnswer(answer, question, maxPoints);
                break;

            default:
                GradeCompositeAllOrNothing(answer, question, maxPoints);
                break;
        }
    }

    private static void GradeSingleChoice(TestAnswer answer, Question question, decimal maxPoints)
    {
        var correctAnswer = question.Answers.FirstOrDefault(a => a.IsCorrect);
        answer.IsCorrect = correctAnswer is not null && string.Equals(answer.AnswerId, correctAnswer.AnswerId, StringComparison.OrdinalIgnoreCase);
        answer.PointsEarned = answer.IsCorrect == true ? maxPoints : 0m;
    }

    private static void GradeMultipleSelect(TestAnswer answer, Question question, decimal maxPoints)
    {
        var correctAnswerIds = question.Answers
            .Where(a => a.IsCorrect)
            .Select(a => a.AnswerId)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var selectedAnswerIds = answer.SelectedOptions
            .Select(o => o.AnswerId)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        answer.IsCorrect = correctAnswerIds.Count > 0 && correctAnswerIds.SetEquals(selectedAnswerIds);
        answer.PointsEarned = answer.IsCorrect == true ? maxPoints : 0m;
    }

    private static void GradeShortAnswer(TestAnswer answer, Question question, decimal maxPoints)
    {
        var correctAnswer = question.Answers.FirstOrDefault(a => a.IsCorrect);
        if (correctAnswer is null || string.IsNullOrWhiteSpace(answer.ShortAnswerText))
        {
            answer.IsCorrect = false;
            answer.PointsEarned = 0m;
            return;
        }

        answer.IsCorrect = NumericShortAnswer.AreEquivalent(answer.ShortAnswerText, correctAnswer.AnswerContent);

        answer.PointsEarned = answer.IsCorrect == true ? maxPoints : 0m;
    }

    private static void GradeComposite(TestAnswer answer, Question question, decimal maxPoints)
    {
        var parts = question.Parts.OrderBy(p => p.PartOrder).ToList();
        bool allTrueFalse = parts.Count > 0 &&
            parts.All(p => string.Equals(NormalizeType(p.PartType), "TRUEFALSE", StringComparison.OrdinalIgnoreCase));

        if (allTrueFalse)
        {
            GradeCompositeAllTrueFalse(answer, question, maxPoints);
        }
        else
        {
            GradeCompositeGeneral(answer, question, maxPoints);
        }
    }

    private static void GradeCompositeAllTrueFalse(
        TestAnswer answer, Question question, decimal maxPoints)
    {
        var parts = question.Parts.OrderBy(p => p.PartOrder).ToList();
        if (parts.Count != 4 || parts.Any(p => NormalizeType(p.PartType) != "TRUEFALSE"))
        {
            throw new InvalidOperationException("TieredTrueFalse scoring requires exactly four TrueFalse parts.");
        }

        int correctCount = 0;
        int totalParts = parts.Count;

        foreach (var part in parts)
        {
            var answerPart = answer.AnswerParts
                .FirstOrDefault(ap => string.Equals(ap.PartId, part.QuestionPartId, StringComparison.OrdinalIgnoreCase));

            if (answerPart is null) continue;

            bool partCorrect = answerPart.BooleanAnswer != null && answerPart.BooleanAnswer == part.CorrectBoolean;
            answerPart.IsCorrect = partCorrect;
            answerPart.PointsEarned = 0m;

            if (partCorrect) correctCount++;
        }

        answer.IsCorrect = correctCount == totalParts && totalParts > 0;

        decimal fraction = correctCount < CompositeAllTfScoreTable.Length
            ? CompositeAllTfScoreTable[correctCount]
            : 1.00m;

        answer.PointsEarned = Math.Round(fraction * maxPoints, 2);
    }

    private static void GradeCompositeGeneral(
        TestAnswer answer, Question question, decimal maxPoints)
    {
        var parts = question.Parts.OrderBy(p => p.PartOrder).ToList();
        decimal totalPartWeight = parts.Sum(p => p.DefaultWeight);
        decimal totalPartPoints = 0m;
        int correctPartCount = 0;

        foreach (var part in parts)
        {
            var answerPart = answer.AnswerParts
                .FirstOrDefault(ap => string.Equals(ap.PartId, part.QuestionPartId, StringComparison.OrdinalIgnoreCase));

            if (answerPart is null) continue;

            bool partCorrect = false;
            var partTypeNormalized = NormalizeType(part.PartType);

            if (partTypeNormalized == "TRUEFALSE")
            {
                partCorrect = answerPart.BooleanAnswer != null && answerPart.BooleanAnswer == part.CorrectBoolean;
            }
            else if (partTypeNormalized == "SHORTANSWER")
            {
                if (!string.IsNullOrWhiteSpace(answerPart.TextAnswer) && !string.IsNullOrWhiteSpace(part.CorrectText))
                {
                    partCorrect = NumericShortAnswer.AreEquivalent(answerPart.TextAnswer, part.CorrectText);
                }
            }
            else if (partTypeNormalized == "NUMERICANSWER")
            {
                if (answerPart.NumericAnswer != null && part.CorrectNumeric != null)
                {
                    decimal diff = Math.Abs(answerPart.NumericAnswer.Value - part.CorrectNumeric.Value);
                    decimal tolerance = part.NumericTolerance ?? 0m;
                    partCorrect = diff <= tolerance;
                }
            }

            answerPart.IsCorrect = partCorrect;

            decimal partMaxPoints = totalPartWeight > 0
                ? Math.Round(part.DefaultWeight / totalPartWeight * maxPoints, 2)
                : 0m;
            answerPart.PointsEarned = partCorrect ? partMaxPoints : 0m;

            totalPartPoints += answerPart.PointsEarned;
            if (partCorrect) correctPartCount++;
        }

        answer.PointsEarned = Math.Min(totalPartPoints, maxPoints);
        answer.IsCorrect = correctPartCount == parts.Count && parts.Count > 0;
    }

    private static void GradeCompositeAllOrNothing(
        TestAnswer answer, Question question, decimal maxPoints)
    {
        var parts = question.Parts.OrderBy(p => p.PartOrder).ToList();
        int correctPartCount = 0;

        foreach (var part in parts)
        {
            var answerPart = answer.AnswerParts
                .FirstOrDefault(ap => string.Equals(ap.PartId, part.QuestionPartId, StringComparison.OrdinalIgnoreCase));

            if (answerPart is null) continue;

            bool partCorrect = false;
            var partTypeNormalized = NormalizeType(part.PartType);

            if (partTypeNormalized == "TRUEFALSE")
            {
                partCorrect = answerPart.BooleanAnswer != null && answerPart.BooleanAnswer == part.CorrectBoolean;
            }
            else if (partTypeNormalized == "SHORTANSWER")
            {
                if (!string.IsNullOrWhiteSpace(answerPart.TextAnswer) && !string.IsNullOrWhiteSpace(part.CorrectText))
                {
                    partCorrect = NumericShortAnswer.AreEquivalent(answerPart.TextAnswer, part.CorrectText);
                }
            }
            else if (partTypeNormalized == "NUMERICANSWER")
            {
                if (answerPart.NumericAnswer != null && part.CorrectNumeric != null)
                {
                    decimal diff = Math.Abs(answerPart.NumericAnswer.Value - part.CorrectNumeric.Value);
                    decimal tolerance = part.NumericTolerance ?? 0m;
                    partCorrect = diff <= tolerance;
                }
            }

            answerPart.IsCorrect = partCorrect;
            answerPart.PointsEarned = 0m;

            if (partCorrect) correctPartCount++;
        }

        answer.IsCorrect = correctPartCount == parts.Count && parts.Count > 0;
        answer.PointsEarned = answer.IsCorrect == true ? maxPoints : 0m;
    }

    private static string NormalizeType(string type)
        => type.Replace("_", "").Replace(" ", "").ToUpperInvariant();
}
