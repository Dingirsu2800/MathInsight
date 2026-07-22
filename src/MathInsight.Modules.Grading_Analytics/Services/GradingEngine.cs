using MathInsight.Modules.Grading_Analytics.Persistence.Entities;

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
        int numCorrect = 0;
        int numIncorrect = 0;
        int numAbandoned = 0;
        decimal sumPointsEarned = 0m;
        decimal sumMaxPoints = 0m;

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

            if (isAbandoned)
            {
                // Abandoned questions are graded as incorrect with 0 points (BR-16b)
                answer.IsCorrect = false;
                answer.PointsEarned = 0m;
                numAbandoned++;
                numIncorrect++;
                continue;
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

            sumPointsEarned += answer.PointsEarned;

            if (answer.IsCorrect == true)
                numCorrect++;
            else
                numIncorrect++;
        }

        // BR-20: score = SUM(points_earned) / SUM(max_points) × 10.0
        decimal score = sumMaxPoints > 0m
            ? Math.Round(sumPointsEarned / sumMaxPoints * 10.0m, 2)
            : 0m;

        // Clamp to 0..10
        score = Math.Max(0m, Math.Min(10m, score));

        return new GradingResult
        {
            Score = score,
            NumCorrect = numCorrect,
            NumIncorrect = numIncorrect,
            NumAbandoned = numAbandoned
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
        var typeNormalized = questionType.Replace("_", "").Replace(" ", "").ToUpperInvariant();
        return typeNormalized switch
        {
            "SINGLECHOICE" => answer.AnswerId is null,
            "TRUEFALSE" => answer.AnswerId is null,
            "MULTIPLESELECT" => answer.SelectedOptions.Count == 0,
            "MULTIPLECHOICE" => answer.SelectedOptions.Count == 0,
            "SHORTANSWER" => string.IsNullOrWhiteSpace(answer.ShortAnswerText),
            "COMPOSITE" => answer.AnswerParts.Count == 0 || answer.AnswerParts.All(p =>
                p.BooleanAnswer == null && 
                string.IsNullOrWhiteSpace(p.TextAnswer) && 
                p.NumericAnswer == null),
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
        var correctAnswer = question.Answers.FirstOrDefault(a => a.IsCorrect);
        if (correctAnswer is null)
        {
            // No correct answer configured — mark incorrect
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
            answer.IsCorrect = false;
            answer.PointsEarned = 0m;
            return;
        }

        answer.IsCorrect = string.Equals(
            answer.ShortAnswerText?.Trim(),
            correctAnswer.AnswerContent.Trim(),
            StringComparison.OrdinalIgnoreCase);

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
            fraction = 0m;
        }
        else if (correctCount == totalParts)
        {
            fraction = 1.00m;
        }
        else if (correctCount >= 1 && correctCount <= 3)
        {
            fraction = CompositeAllTfScoreTable[correctCount];
        }
        else
        {
            fraction = 0.50m;
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
