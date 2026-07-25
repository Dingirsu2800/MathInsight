using MathInsight.Modules.Grading_Analytics.Persistence.Entities;
using MathInsight.Shared.Questions;
using MathInsight.Shared.Scoring;

namespace MathInsight.Modules.Grading_Analytics.Services;

public class GradingEngine : IGradingEngine
{
    public GradingResult Grade(TestSession session)
    {
        var correct = 0;
        var incorrect = 0;
        var abandoned = 0;
        var effectiveEarned = 0m;
        var totalMax = 0m;

        foreach (var answer in session.TestAnswers)
        {
            var snapshot = answer.Snapshot;
            var questionType = snapshot?.QuestionType ?? answer.Question.QuestionType;
            var maxPoints = snapshot is null ? answer.Question.DefaultWeight : answer.MaxPointsSnapshot;
            totalMax += maxPoints;

            var isAbandoned = IsAbandoned(answer, questionType);
            if (isAbandoned)
            {
                answer.IsCorrect = false;
                answer.PointsEarned = 0m;
            }
            else if (snapshot is not null)
            {
                GradeSnapshot(answer, snapshot, maxPoints);
            }
            else
            {
                GradeLegacy(answer, answer.Question, maxPoints);
            }

            effectiveEarned += answer.IsScoreInvalidated ? maxPoints : answer.PointsEarned;
            if (answer.IsScoreInvalidated)
                continue;

            if (isAbandoned)
            {
                abandoned++;
                incorrect++;
            }
            else if (answer.IsCorrect == true)
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

    internal static bool IsAbandoned(TestAnswer answer, string questionType)
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

    private static void GradeSnapshot(TestAnswer answer, QuestionSnapshotV2 snapshot, decimal maxPoints)
    {
        switch (NormalizeType(snapshot.QuestionType))
        {
            case "SINGLECHOICE":
            case "TRUEFALSE":
                var correctAnswer = snapshot.Answers.FirstOrDefault(option => option.IsCorrect);
                answer.IsCorrect = correctAnswer is not null && answer.AnswerId == correctAnswer.AnswerId;
                answer.PointsEarned = answer.IsCorrect == true ? maxPoints : 0m;
                return;

            case "MULTIPLESELECT":
            case "MULTIPLECHOICE":
                var expected = snapshot.Answers.Where(option => option.IsCorrect).Select(option => option.AnswerId).ToHashSet(StringComparer.OrdinalIgnoreCase);
                var selected = answer.SelectedOptions.Select(option => option.AnswerId).ToHashSet(StringComparer.OrdinalIgnoreCase);
                answer.IsCorrect = expected.SetEquals(selected);
                answer.PointsEarned = answer.IsCorrect == true ? maxPoints : 0m;
                return;

            case "SHORTANSWER":
                var expectedText = snapshot.Answers.FirstOrDefault(option => option.IsCorrect)?.AnswerContent;
                answer.IsCorrect = !string.IsNullOrWhiteSpace(expectedText) &&
                    string.Equals(answer.ShortAnswerText?.Trim(), expectedText.Trim(), StringComparison.OrdinalIgnoreCase);
                answer.PointsEarned = answer.IsCorrect == true ? maxPoints : 0m;
                return;

            case "COMPOSITE":
                GradeCompositeSnapshot(answer, snapshot.Parts, maxPoints);
                return;

            default:
                answer.IsCorrect = false;
                answer.PointsEarned = 0m;
                return;
        }
    }

    private static void GradeCompositeSnapshot(
        TestAnswer answer,
        IReadOnlyList<QuestionPartSnapshot> parts,
        decimal maxPoints)
    {
        var ordered = parts.OrderBy(part => part.PartOrder).ToList();
        var correctCount = 0;
        foreach (var part in ordered)
        {
            var submitted = answer.AnswerParts.FirstOrDefault(item => item.PartId == part.PartId);
            if (submitted is null)
                continue;
            submitted.IsCorrect = IsPartCorrect(submitted, part);
            if (submitted.IsCorrect == true)
                correctCount++;
        }

        answer.IsCorrect = ordered.Count > 0 && correctCount == ordered.Count;
        if (answer.ScoringRuleSnapshot == ScoringRules.TieredTrueFalse)
        {
            if (ordered.Count != 4 || ordered.Any(part => NormalizeType(part.PartType) != "TRUEFALSE"))
                throw new InvalidOperationException(
                    "TieredTrueFalse scoring requires exactly four TrueFalse parts.");

            var fractions = new[] { 0m, 0.10m, 0.25m, 0.50m, 1m };
            answer.PointsEarned = Math.Round(maxPoints * fractions[correctCount], 2);
            foreach (var submitted in answer.AnswerParts)
                submitted.PointsEarned = 0m;
            return;
        }

        if (answer.ScoringRuleSnapshot != ScoringRules.WeightedParts)
            throw new InvalidOperationException(
                $"Unsupported composite scoring rule '{answer.ScoringRuleSnapshot}'.");

        var allocations = ScoringAllocator.Allocate(
            maxPoints,
            ordered.Select(part => new WeightedScoreItem(part.PartId, part.DefaultWeight, part.PartOrder)).ToList());
        foreach (var submitted in answer.AnswerParts)
            submitted.PointsEarned = submitted.IsCorrect == true && allocations.TryGetValue(submitted.PartId, out var points) ? points : 0m;
        answer.PointsEarned = Math.Min(maxPoints, answer.AnswerParts.Sum(part => part.PointsEarned));
    }

    private static bool IsPartCorrect(TestAnswerPart submitted, QuestionPartSnapshot part)
        => NormalizeType(part.PartType) switch
        {
            "TRUEFALSE" => submitted.BooleanAnswer is not null && submitted.BooleanAnswer == part.CorrectBoolean,
            "SHORTANSWER" => !string.IsNullOrWhiteSpace(submitted.TextAnswer) &&
                             !string.IsNullOrWhiteSpace(part.CorrectText) &&
                             string.Equals(submitted.TextAnswer.Trim(), part.CorrectText.Trim(), StringComparison.OrdinalIgnoreCase),
            "NUMERICANSWER" => submitted.NumericAnswer is not null && part.CorrectNumeric is not null &&
                               Math.Abs(submitted.NumericAnswer.Value - part.CorrectNumeric.Value) <= (part.NumericTolerance ?? 0m),
            _ => false
        };

    private static void GradeLegacy(TestAnswer answer, Question question, decimal maxPoints)
    {
        switch (NormalizeType(question.QuestionType))
        {
            case "SINGLECHOICE":
            case "TRUEFALSE":
                answer.IsCorrect = answer.AnswerId == question.Answers.FirstOrDefault(option => option.IsCorrect)?.AnswerId;
                break;
            case "MULTIPLESELECT":
            case "MULTIPLECHOICE":
                answer.IsCorrect = question.Answers.Where(option => option.IsCorrect).Select(option => option.AnswerId).ToHashSet()
                    .SetEquals(answer.SelectedOptions.Select(option => option.AnswerId));
                break;
            case "SHORTANSWER":
                var expected = question.Answers.FirstOrDefault(option => option.IsCorrect)?.AnswerContent;
                answer.IsCorrect = !string.IsNullOrWhiteSpace(expected) &&
                    string.Equals(answer.ShortAnswerText?.Trim(), expected.Trim(), StringComparison.OrdinalIgnoreCase);
                break;
            case "COMPOSITE":
                var snapshot = new QuestionSnapshotV2(
                    question.QuestionId, question.QuestionType, question.DifficultyId, 0, question.DefaultWeight, [], [],
                    question.Parts.Select(part => new QuestionPartSnapshot(
                        part.QuestionPartId, part.PartOrder, part.PartLabel, part.Content, part.PartType,
                        part.CorrectBoolean, part.CorrectText, part.CorrectNumeric, part.NumericTolerance,
                        part.Explanation, part.DefaultWeight)).ToList());
                answer.ScoringRuleSnapshot = question.Parts.All(part => NormalizeType(part.PartType) == "TRUEFALSE") && question.Parts.Count == 4
                    ? ScoringRules.TieredTrueFalse
                    : ScoringRules.WeightedParts;
                GradeCompositeSnapshot(answer, snapshot.Parts, maxPoints);
                return;
            default:
                answer.IsCorrect = false;
                break;
        }
        answer.PointsEarned = answer.IsCorrect == true ? maxPoints : 0m;
    }

    private static string NormalizeType(string value)
        => value.Replace("_", string.Empty).Replace(" ", string.Empty).ToUpperInvariant();
}
