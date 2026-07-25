using MathInsight.Modules.Grading_Analytics.Persistence.Entities;
using MathInsight.Shared.Questions;
using MathInsight.Shared.Scoring;

namespace MathInsight.Modules.Grading_Analytics.Services;

/// <summary>
/// Per-question-type grading logic.
/// Grades all answers for a session synchronously by mutating IsCorrect and PointsEarned
/// on TestAnswer (and TestAnswerPart for COMPOSITE) entities in-place.
///
/// Supported question types:
///   SINGLE_CHOICE, TRUE_FALSE, MULTIPLE_SELECT, SHORT_ANSWER, COMPOSITE
///
/// Scoring source:
///   Points per question come from TestQuestion.MaxPointsSnapshot (not Question.DefaultWeight).
///   Question.DefaultWeight is a weight coefficient, not a point value.
///   Part-level distribution uses QuestionPart.DefaultWeight ratios within a Composite.
///
/// Score invalidation:
///   When TestQuestion.IsScoreInvalidated == true, the question is awarded full MaxPointsSnapshot
///   without running grading logic (EffectivePoints = MaxPointsSnapshot).
///
/// ScoringRule routing:
///   If TestQuestion.ScoringRuleSnapshot is set, it takes priority:
///     AllOrNothing      → all-or-nothing grading
///     TieredTrueFalse   → BR-23 non-linear table
///     WeightedParts     → per-part weighted scoring
///   Otherwise falls back to QuestionType-based routing (backward compat).
///
/// Special rules:
///   - BR-23: COMPOSITE all-TRUE_FALSE parts use non-linear scoring table.
///   - BR-20: score = SUM(points_earned) / SUM(max_points) × MaxScore
///   - BR-16b: Abandoned detection is question-type-specific.
/// </summary>
public class GradingEngine : IGradingEngine
{
    // BR-23 non-linear scoring table for COMPOSITE all-TRUE_FALSE / TieredTrueFalse.
    // Index = number of correct parts → fraction of max_points.
    // 0 correct = 0.00, 1 = 0.10, 2 = 0.25, 3 = 0.50, N (all) = 1.00.
    private static readonly decimal[] CompositeAllTfScoreTable = [0.00m, 0.10m, 0.25m, 0.50m];

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

            // Resolve max points: prefer TestQuestion.MaxPointsSnapshot (new schema),
            // fall back to Question.DefaultWeight for backward compat during migration.
            decimal maxPoints = testQuestion?.MaxPointsSnapshot ?? question.DefaultWeight;
            sumMaxPoints += maxPoints;

            // ── Score invalidation ─────────────────────────────────────────
            // When a report confirms the question is erroneous, award full points
            // without running grading logic. PointsEarned is NOT overwritten on
            // TestAnswer — we use EffectivePoints at score calculation time.
            if (testQuestion?.IsScoreInvalidated == true)
            {
                // Keep original PointsEarned for audit; effective = MaxPointsSnapshot.
                // Mark as null (invalidated, neither correct nor incorrect).
                answer.IsCorrect = null;
                answer.PointsEarned = maxPoints;
                sumPointsEarned += maxPoints;
                // Don't count invalidated questions in correct/incorrect/abandoned
                continue;
            }

            bool isAbandoned = IsAbandoned(answer, question.QuestionType);

            var isAbandoned = IsAbandoned(answer, questionType);
            if (isAbandoned)
            {
                answer.IsCorrect = false;
                answer.PointsEarned = 0m;
            }

            // ── Determine grading strategy ─────────────────────────────────
            // Priority: ScoringRuleSnapshot > QuestionType
            var scoringRule = testQuestion?.ScoringRuleSnapshot;

            if (!string.IsNullOrEmpty(scoringRule))
            {
                GradeByScoringRule(answer, question, maxPoints, scoringRule);
            }
            else
            {
                GradeByQuestionType(answer, question, maxPoints);
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

    /// <summary>
    /// Routes grading based on ScoringRuleSnapshot (new schema path).
    /// </summary>
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
                // Unknown scoring rule — fall back to QuestionType routing
                GradeByQuestionType(answer, question, maxPoints);
                break;
        }
    }

    /// <summary>
    /// Routes grading based on QuestionType (backward-compatible path).
    /// </summary>
    private static void GradeByQuestionType(TestAnswer answer, Question question, decimal maxPoints)
    {
        var typeNormalized = question.QuestionType.Replace("_", "").Replace(" ", "").ToUpperInvariant();

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
                // Unknown question type — treat as incorrect
                answer.IsCorrect = false;
                answer.PointsEarned = 0m;
                break;
        }
    }

    /// <summary>
    /// Determines if a question answer is abandoned/unanswered per BR-16b.
    /// Rules vary by question type.
    /// </summary>
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

    /// <summary>
    /// AllOrNothing: correct = full points, incorrect = 0.
    /// Used for SINGLE_CHOICE, TRUE_FALSE, MULTIPLE_SELECT, SHORT_ANSWER
    /// and when ScoringRuleSnapshot = "AllOrNothing".
    /// Dispatches to the appropriate correctness check based on QuestionType.
    /// </summary>
    private static void GradeAllOrNothing(TestAnswer answer, Question question, decimal maxPoints)
    {
        var typeNormalized = question.QuestionType.Replace("_", "").Replace(" ", "").ToUpperInvariant();

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
                // For COMPOSITE with AllOrNothing, grade each part and award full if all correct
                GradeCompositeAllOrNothing(answer, question, maxPoints);
                break;
        }
    }

    /// <summary>
    /// SINGLE_CHOICE / TRUE_FALSE: compare student's answer_id to the correct answer.
    /// </summary>
    private static void GradeSingleChoice(TestAnswer answer, Question question, decimal maxPoints)
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

        answer.IsCorrect = answer.AnswerId == correctAnswer.AnswerId;
        answer.PointsEarned = answer.IsCorrect == true ? maxPoints : 0m;
    }

    /// <summary>
    /// MULTIPLE_SELECT: all correct options must be selected AND no incorrect options.
    /// </summary>
    private static void GradeMultipleSelect(TestAnswer answer, Question question, decimal maxPoints)
    {
        var correctAnswerIds = question.Answers
            .Where(a => a.IsCorrect)
            .Select(a => a.AnswerId)
            .ToHashSet();

        var selectedAnswerIds = answer.SelectedOptions
            .Select(o => o.AnswerId)
            .ToHashSet();

        // All correct options selected AND no incorrect options selected
        answer.IsCorrect = correctAnswerIds.SetEquals(selectedAnswerIds);
        answer.PointsEarned = answer.IsCorrect == true ? maxPoints : 0m;
    }

    /// <summary>
    /// SHORT_ANSWER: case-insensitive string match against correct Answer.AnswerContent.
    /// </summary>
    private static void GradeShortAnswer(TestAnswer answer, Question question, decimal maxPoints)
    {
        var correctAnswer = question.Answers.FirstOrDefault(a => a.IsCorrect);
        if (correctAnswer is null || string.IsNullOrWhiteSpace(answer.ShortAnswerText))
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

        answer.PointsEarned = answer.IsCorrect == true ? maxPoints : 0m;
    }

    /// <summary>
    /// COMPOSITE grading — dispatches to BR-23 non-linear table or general per-part grading.
    /// </summary>
    private static void GradeComposite(TestAnswer answer, Question question, decimal maxPoints)
    {
        var parts = question.Parts.OrderBy(p => p.PartOrder).ToList();
        bool allTrueFalse = parts.Count > 0 &&
            parts.All(p => string.Equals(p.PartType.Replace("_", ""), "TrueFalse", StringComparison.OrdinalIgnoreCase));

        if (allTrueFalse)
        {
            GradeCompositeAllTrueFalse(answer, question, maxPoints);
        }
        else
        {
            GradeCompositeGeneral(answer, question, maxPoints);
        }
    }

    /// <summary>
    /// COMPOSITE all-TRUE_FALSE (BR-23): count correct parts → non-linear table.
    /// TestAnswer.points_earned = source of truth.
    /// TestAnswerPart.is_correct = recorded individually for solution display.
    /// TestAnswerPart.points_earned = 0 (NOT used for score calculation).
    /// </summary>
    private static void GradeCompositeAllTrueFalse(
        TestAnswer answer, Question question, decimal maxPoints)
    {
        var parts = question.Parts.OrderBy(p => p.PartOrder).ToList();
        int correctCount = 0;
        int totalParts = parts.Count;

        foreach (var part in parts)
        {
            var answerPart = answer.AnswerParts
                .FirstOrDefault(ap => ap.PartId == part.QuestionPartId);

            if (answerPart is null) continue;

            // TrueFalse: compare BooleanAnswer with CorrectBoolean
            bool partCorrect = answerPart.BooleanAnswer != null && answerPart.BooleanAnswer == part.CorrectBoolean;

            answerPart.IsCorrect = partCorrect;
            // Child part points_earned = 0 for all-TRUE_FALSE mode (spec rule)
            answerPart.PointsEarned = 0m;

            if (partCorrect) correctCount++;
        }

        // Parent is_correct = true only when ALL parts are correct
        answer.IsCorrect = correctCount == totalParts && totalParts > 0;

        // BR-23 non-linear table: 0→0, 1→0.10, 2→0.25, 3→0.50, N→1.00
        decimal fraction;
        if (correctCount == 0)
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

        answer.PointsEarned = Math.Round(fraction * maxPoints, 2);
    }

    /// <summary>
    /// COMPOSITE general (mixed part types) / WeightedParts: grade each QuestionPart individually.
    /// Part points are distributed based on DefaultWeight ratios within the Composite.
    /// Parent points_earned = sum of part points earned, capped at maxPoints.
    /// </summary>
    private static void GradeCompositeGeneral(
        TestAnswer answer, Question question, decimal maxPoints)
    {
        var parts = question.Parts.OrderBy(p => p.PartOrder).ToList();

        // Calculate total weight of all parts for proportional distribution
        decimal totalPartWeight = parts.Sum(p => p.DefaultWeight);

        decimal totalPartPoints = 0m;
        int correctPartCount = 0;

        foreach (var part in parts)
        {
            var answerPart = answer.AnswerParts
                .FirstOrDefault(ap => ap.PartId == part.QuestionPartId);

            if (answerPart is null) continue;

            bool partCorrect = false;

            var partTypeNormalized = part.PartType.Replace("_", "").Replace(" ", "").ToUpperInvariant();

            if (partTypeNormalized == "TRUEFALSE")
            {
                partCorrect = answerPart.BooleanAnswer != null && answerPart.BooleanAnswer == part.CorrectBoolean;
            }
            else if (partTypeNormalized == "SHORTANSWER")
            {
                if (!string.IsNullOrWhiteSpace(answerPart.TextAnswer) && !string.IsNullOrWhiteSpace(part.CorrectText))
                {
                    partCorrect = string.Equals(
                        answerPart.TextAnswer.Trim(),
                        part.CorrectText.Trim(),
                        StringComparison.OrdinalIgnoreCase);
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

            // Distribute maxPoints proportionally by weight
            decimal partMaxPoints = totalPartWeight > 0
                ? Math.Round(part.DefaultWeight / totalPartWeight * maxPoints, 2)
                : 0m;
            answerPart.PointsEarned = partCorrect ? partMaxPoints : 0m;

            totalPartPoints += answerPart.PointsEarned;
            if (partCorrect) correctPartCount++;
        }

        // Parent score = sum of part points earned, capped at maxPoints
        answer.PointsEarned = Math.Min(totalPartPoints, maxPoints);

        // Parent is_correct = true only when ALL parts are correct
        answer.IsCorrect = correctPartCount == parts.Count && parts.Count > 0;
    }

    /// <summary>
    /// COMPOSITE AllOrNothing: grade each part for correctness, but award full maxPoints only if ALL correct.
    /// </summary>
    private static void GradeCompositeAllOrNothing(
        TestAnswer answer, Question question, decimal maxPoints)
    {
        var parts = question.Parts.OrderBy(p => p.PartOrder).ToList();
        int correctPartCount = 0;

        foreach (var part in parts)
        {
            var answerPart = answer.AnswerParts
                .FirstOrDefault(ap => ap.PartId == part.QuestionPartId);

            if (answerPart is null) continue;

            bool partCorrect = false;
            var partTypeNormalized = part.PartType.Replace("_", "").Replace(" ", "").ToUpperInvariant();

            if (partTypeNormalized == "TRUEFALSE")
            {
                partCorrect = answerPart.BooleanAnswer != null && answerPart.BooleanAnswer == part.CorrectBoolean;
            }
            else if (partTypeNormalized == "SHORTANSWER")
            {
                if (!string.IsNullOrWhiteSpace(answerPart.TextAnswer) && !string.IsNullOrWhiteSpace(part.CorrectText))
                {
                    partCorrect = string.Equals(
                        answerPart.TextAnswer.Trim(),
                        part.CorrectText.Trim(),
                        StringComparison.OrdinalIgnoreCase);
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
            answerPart.PointsEarned = 0m; // individual part points not used in AllOrNothing

            if (partCorrect) correctPartCount++;
        }

        answer.IsCorrect = correctPartCount == parts.Count && parts.Count > 0;
        answer.PointsEarned = answer.IsCorrect == true ? maxPoints : 0m;
    }
}
