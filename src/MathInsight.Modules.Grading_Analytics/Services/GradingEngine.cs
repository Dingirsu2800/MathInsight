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
/// Special rules:
///   - BR-23: COMPOSITE all-TRUE_FALSE parts use non-linear scoring table.
///   - BR-20: score = SUM(points_earned) / SUM(max_points) × 10.0
///   - BR-16b: Abandoned detection is question-type-specific.
/// </summary>
public class GradingEngine : IGradingEngine
{
    // BR-23 non-linear scoring table for COMPOSITE all-TRUE_FALSE.
    // Index = number of correct parts → fraction of default_point.
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
            var defaultPoint = question.DefaultPoint;
            sumMaxPoints += defaultPoint;

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

            switch (question.QuestionType)
            {
                case "SINGLE_CHOICE":
                case "TRUE_FALSE":
                    GradeSingleChoice(answer, question);
                    break;

                case "MULTIPLE_SELECT":
                    GradeMultipleSelect(answer, question);
                    break;

                case "SHORT_ANSWER":
                    GradeShortAnswer(answer, question);
                    break;

                case "COMPOSITE":
                    GradeComposite(answer, question);
                    break;

                default:
                    // Unknown question type — treat as incorrect
                    answer.IsCorrect = false;
                    answer.PointsEarned = 0m;
                    break;
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
    /// Determines if a question answer is abandoned/unanswered per BR-16b.
    /// Rules vary by question type.
    /// </summary>
    private static bool IsAbandoned(TestAnswer answer, string questionType)
    {
        return questionType switch
        {
            "SINGLE_CHOICE" => answer.AnswerId is null,
            "TRUE_FALSE" => answer.AnswerId is null,
            "MULTIPLE_SELECT" => answer.SelectedOptions.Count == 0,
            "SHORT_ANSWER" => string.IsNullOrWhiteSpace(answer.ShortAnswerText),
            "COMPOSITE" => answer.AnswerParts.All(p =>
                string.IsNullOrWhiteSpace(p.StudentAnswer)),
            _ => true
        };
    }

    /// <summary>
    /// SINGLE_CHOICE / TRUE_FALSE: compare student's answer_id to the correct answer.
    /// </summary>
    private static void GradeSingleChoice(TestAnswer answer, Question question)
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
        answer.PointsEarned = answer.IsCorrect == true ? question.DefaultPoint : 0m;
    }

    /// <summary>
    /// MULTIPLE_SELECT: all correct options must be selected AND no incorrect options.
    /// </summary>
    private static void GradeMultipleSelect(TestAnswer answer, Question question)
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
        answer.PointsEarned = answer.IsCorrect == true ? question.DefaultPoint : 0m;
    }

    /// <summary>
    /// SHORT_ANSWER: case-insensitive string match against correct Answer.AnswerContent.
    /// </summary>
    private static void GradeShortAnswer(TestAnswer answer, Question question)
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

        answer.PointsEarned = answer.IsCorrect == true ? question.DefaultPoint : 0m;
    }

    /// <summary>
    /// COMPOSITE grading — dispatches to BR-23 non-linear table or general per-part grading.
    /// </summary>
    private static void GradeComposite(TestAnswer answer, Question question)
    {
        var parts = question.Parts.OrderBy(p => p.PartOrder).ToList();
        bool allTrueFalse = parts.Count > 0 &&
            parts.All(p => string.Equals(p.PartType, "TRUE_FALSE", StringComparison.OrdinalIgnoreCase));

        if (allTrueFalse)
        {
            GradeCompositeAllTrueFalse(answer, question, parts);
        }
        else
        {
            GradeCompositeGeneral(answer, question, parts);
        }
    }

    /// <summary>
    /// COMPOSITE all-TRUE_FALSE (BR-23): count correct parts → non-linear table.
    /// TestAnswer.points_earned = source of truth.
    /// TestAnswerPart.is_correct = recorded individually for solution display.
    /// TestAnswerPart.points_earned = 0 (NOT used for score calculation).
    /// </summary>
    private static void GradeCompositeAllTrueFalse(
        TestAnswer answer, Question question, List<QuestionPart> parts)
    {
        int correctCount = 0;
        int totalParts = parts.Count;

        foreach (var part in parts)
        {
            var answerPart = answer.AnswerParts
                .FirstOrDefault(ap => ap.QuestionPartId == part.QuestionPartId);

            if (answerPart is null) continue;

            // TRUE_FALSE: compare student answer to answer key (case-insensitive)
            bool partCorrect = string.Equals(
                answerPart.StudentAnswer?.Trim(),
                part.AnswerKey.Trim(),
                StringComparison.OrdinalIgnoreCase);

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
            // For > 3 correct but not all: interpolate linearly between 0.50 and 1.00
            // This handles cases with more than 4 parts where BR-23 table only defines up to 3.
            // Spec says 0→0, 1→0.10, 2→0.25, 3→0.50, N(all)→1.00.
            // For 4..N-1 correct parts, use 0.50 (same as 3 correct — conservative approach).
            fraction = 0.50m;
        }

        answer.PointsEarned = Math.Round(fraction * question.DefaultPoint, 2);
    }

    /// <summary>
    /// COMPOSITE general (mixed part types): grade each QuestionPart individually.
    /// Parent points_earned = sum of part points earned.
    /// </summary>
    private static void GradeCompositeGeneral(
        TestAnswer answer, Question question, List<QuestionPart> parts)
    {
        decimal totalPartPoints = 0m;
        int correctPartCount = 0;

        foreach (var part in parts)
        {
            var answerPart = answer.AnswerParts
                .FirstOrDefault(ap => ap.QuestionPartId == part.QuestionPartId);

            if (answerPart is null) continue;

            bool partCorrect;

            if (string.IsNullOrWhiteSpace(answerPart.StudentAnswer))
            {
                // No answer for this part — incorrect
                partCorrect = false;
            }
            else
            {
                // Case-insensitive string match for all part types
                partCorrect = string.Equals(
                    answerPart.StudentAnswer.Trim(),
                    part.AnswerKey.Trim(),
                    StringComparison.OrdinalIgnoreCase);
            }

            answerPart.IsCorrect = partCorrect;
            answerPart.PointsEarned = partCorrect ? part.PointValue : 0m;

            totalPartPoints += answerPart.PointsEarned;
            if (partCorrect) correctPartCount++;
        }

        // Parent score = sum of part points earned, capped at question's default_point
        answer.PointsEarned = Math.Min(totalPartPoints, question.DefaultPoint);

        // Parent is_correct = true only when ALL parts are correct
        answer.IsCorrect = correctPartCount == parts.Count && parts.Count > 0;
    }
}
