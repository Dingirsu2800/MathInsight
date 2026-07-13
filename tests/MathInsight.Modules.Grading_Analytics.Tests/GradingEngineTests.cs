using MathInsight.Modules.Grading_Analytics.Services;

namespace MathInsight.Modules.Grading_Analytics.Tests;

/// <summary>
/// Unit tests for GradingEngine — validates per-question-type grading logic,
/// BR-23 non-linear composite scoring, BR-16b abandoned detection, and BR-20 score formula.
/// All tests use pure in-memory entity graphs (no database).
/// </summary>
public class GradingEngineTests
{
    private readonly GradingEngine _engine = new();

    // ── SINGLE_CHOICE ─────────────────────────────────────────────────────

    [Fact]
    public void SingleChoice_Correct_SetsIsCorrectTrue_And_PointsEarnedEqualDefaultPoint()
    {
        // Arrange
        var correctId = Guid.NewGuid();
        var session = TestDataBuilder.CreateSession();
        TestDataBuilder.AddSingleChoiceAnswer(session, defaultPoint: 2.0m, correctId, studentAnswerId: correctId);

        // Act
        var result = _engine.Grade(session);

        // Assert
        var answer = session.TestAnswers.First();
        Assert.True(answer.IsCorrect);
        Assert.Equal(2.0m, answer.PointsEarned);
        Assert.Equal(1, result.NumCorrect);
        Assert.Equal(0, result.NumIncorrect);
        Assert.Equal(0, result.NumAbandoned);
        Assert.Equal(10.0m, result.Score); // 2/2 * 10 = 10
    }

    [Fact]
    public void SingleChoice_Incorrect_SetsIsCorrectFalse_And_PointsEarnedZero()
    {
        // Arrange
        var correctId = Guid.NewGuid();
        var wrongId = Guid.NewGuid();
        var session = TestDataBuilder.CreateSession();
        TestDataBuilder.AddSingleChoiceAnswer(session, defaultPoint: 2.0m, correctId, studentAnswerId: wrongId);

        // Act
        var result = _engine.Grade(session);

        // Assert
        var answer = session.TestAnswers.First();
        Assert.False(answer.IsCorrect);
        Assert.Equal(0m, answer.PointsEarned);
        Assert.Equal(0, result.NumCorrect);
        Assert.Equal(1, result.NumIncorrect);
    }

    // ── MULTIPLE_SELECT ───────────────────────────────────────────────────

    [Fact]
    public void MultipleSelect_AllCorrectSelected_NoIncorrect_SetsIsCorrectTrue()
    {
        // Arrange
        var id1 = Guid.NewGuid();
        var id2 = Guid.NewGuid();
        var session = TestDataBuilder.CreateSession();
        TestDataBuilder.AddMultipleSelectAnswer(
            session,
            defaultPoint: 3.0m,
            correctAnswerIds: [id1, id2],
            selectedAnswerIds: [id1, id2]);

        // Act
        var result = _engine.Grade(session);

        // Assert
        var answer = session.TestAnswers.First();
        Assert.True(answer.IsCorrect);
        Assert.Equal(3.0m, answer.PointsEarned);
        Assert.Equal(1, result.NumCorrect);
    }

    [Fact]
    public void MultipleSelect_Partial_SetsIsCorrectFalse_And_PointsEarnedZero()
    {
        // Arrange: only select 1 of 2 correct answers
        var id1 = Guid.NewGuid();
        var id2 = Guid.NewGuid();
        var session = TestDataBuilder.CreateSession();
        TestDataBuilder.AddMultipleSelectAnswer(
            session,
            defaultPoint: 3.0m,
            correctAnswerIds: [id1, id2],
            selectedAnswerIds: [id1]); // Partial — missing id2

        // Act
        var result = _engine.Grade(session);

        // Assert
        var answer = session.TestAnswers.First();
        Assert.False(answer.IsCorrect);
        Assert.Equal(0m, answer.PointsEarned);
        Assert.Equal(0, result.NumCorrect);
        Assert.Equal(1, result.NumIncorrect);
    }

    [Fact]
    public void MultipleSelect_ExtraIncorrect_SetsIsCorrectFalse()
    {
        // Arrange: select correct + 1 wrong
        var id1 = Guid.NewGuid();
        var extraWrong = Guid.NewGuid();
        var session = TestDataBuilder.CreateSession();
        TestDataBuilder.AddMultipleSelectAnswer(
            session,
            defaultPoint: 3.0m,
            correctAnswerIds: [id1],
            selectedAnswerIds: [id1, extraWrong]);

        // Act
        var result = _engine.Grade(session);

        // Assert
        var answer = session.TestAnswers.First();
        Assert.False(answer.IsCorrect);
        Assert.Equal(0m, answer.PointsEarned);
    }

    // ── SHORT_ANSWER ──────────────────────────────────────────────────────

    [Fact]
    public void ShortAnswer_CaseInsensitiveMatch_SetsIsCorrectTrue()
    {
        // Arrange
        var session = TestDataBuilder.CreateSession();
        TestDataBuilder.AddShortAnswer(session, defaultPoint: 1.5m, correctAnswer: "42", studentAnswer: "42");

        // Act
        var result = _engine.Grade(session);

        // Assert
        var answer = session.TestAnswers.First();
        Assert.True(answer.IsCorrect);
        Assert.Equal(1.5m, answer.PointsEarned);
    }

    [Fact]
    public void ShortAnswer_DifferentCase_StillCorrect()
    {
        // Arrange
        var session = TestDataBuilder.CreateSession();
        TestDataBuilder.AddShortAnswer(session, defaultPoint: 2.0m, correctAnswer: "Pi", studentAnswer: "pi");

        // Act
        var result = _engine.Grade(session);

        // Assert
        var answer = session.TestAnswers.First();
        Assert.True(answer.IsCorrect);
        Assert.Equal(2.0m, answer.PointsEarned);
    }

    [Fact]
    public void ShortAnswer_WithWhitespace_StillCorrect()
    {
        // Arrange
        var session = TestDataBuilder.CreateSession();
        TestDataBuilder.AddShortAnswer(session, defaultPoint: 2.0m, correctAnswer: "answer", studentAnswer: "  answer  ");

        // Act
        var result = _engine.Grade(session);

        // Assert
        Assert.True(session.TestAnswers.First().IsCorrect);
    }

    [Fact]
    public void ShortAnswer_Incorrect_SetsIsCorrectFalse()
    {
        // Arrange
        var session = TestDataBuilder.CreateSession();
        TestDataBuilder.AddShortAnswer(session, defaultPoint: 2.0m, correctAnswer: "42", studentAnswer: "43");

        // Act
        _engine.Grade(session);

        // Assert
        var answer = session.TestAnswers.First();
        Assert.False(answer.IsCorrect);
        Assert.Equal(0m, answer.PointsEarned);
    }

    // ── ABANDONED (BR-16b) ────────────────────────────────────────────────

    [Fact]
    public void Abandoned_SingleChoice_NullAnswerId_IsIncorrect_And_CountedAsAbandoned()
    {
        // Arrange: student did not select any answer (AnswerId = null)
        var correctId = Guid.NewGuid();
        var session = TestDataBuilder.CreateSession();
        TestDataBuilder.AddSingleChoiceAnswer(session, defaultPoint: 1.0m, correctId, studentAnswerId: null);

        // Act
        var result = _engine.Grade(session);

        // Assert
        var answer = session.TestAnswers.First();
        Assert.False(answer.IsCorrect);
        Assert.Equal(0m, answer.PointsEarned);
        Assert.Equal(1, result.NumAbandoned);
        Assert.Equal(1, result.NumIncorrect); // Abandoned counts as incorrect too
        Assert.Equal(0, result.NumCorrect);
    }

    [Fact]
    public void Abandoned_MultipleSelect_NoOptions_CountedAsAbandoned()
    {
        // Arrange: student did not select any options
        var id1 = Guid.NewGuid();
        var session = TestDataBuilder.CreateSession();
        TestDataBuilder.AddMultipleSelectAnswer(
            session,
            defaultPoint: 2.0m,
            correctAnswerIds: [id1],
            selectedAnswerIds: []); // No selections

        // Act
        var result = _engine.Grade(session);

        // Assert
        Assert.Equal(1, result.NumAbandoned);
        Assert.False(session.TestAnswers.First().IsCorrect);
    }

    [Fact]
    public void Abandoned_ShortAnswer_Null_CountedAsAbandoned()
    {
        // Arrange: short answer text is null
        var session = TestDataBuilder.CreateSession();
        TestDataBuilder.AddShortAnswer(session, defaultPoint: 1.0m, correctAnswer: "42", studentAnswer: null);

        // Act
        var result = _engine.Grade(session);

        // Assert
        Assert.Equal(1, result.NumAbandoned);
    }

    [Fact]
    public void Abandoned_ShortAnswer_Whitespace_CountedAsAbandoned()
    {
        // Arrange: short answer text is whitespace only
        var session = TestDataBuilder.CreateSession();
        TestDataBuilder.AddShortAnswer(session, defaultPoint: 1.0m, correctAnswer: "42", studentAnswer: "   ");

        // Act
        var result = _engine.Grade(session);

        // Assert
        Assert.Equal(1, result.NumAbandoned);
    }

    // ── COMPOSITE ALL-TRUE_FALSE (BR-23) ──────────────────────────────────

    [Fact]
    public void CompositeAllTF_0Correct_PointsEarnedZero()
    {
        // BR-23: 0 correct → 0
        var session = TestDataBuilder.CreateSession();
        TestDataBuilder.AddCompositeAllTrueFalse(
            session,
            defaultPoint: 2.0m,
            parts:
            [
                ("True", "False"),  // wrong
                ("False", "True"),  // wrong
                ("True", "False"),  // wrong
                ("False", "True"),  // wrong
            ]);

        // Act
        var result = _engine.Grade(session);

        // Assert
        var answer = session.TestAnswers.First();
        Assert.False(answer.IsCorrect);
        Assert.Equal(0m, answer.PointsEarned);
    }

    [Fact]
    public void CompositeAllTF_1ofN_Correct_PointsEarned_0_10_Times_DefaultPoint()
    {
        // BR-23: 1 correct → 0.10 × dp
        var session = TestDataBuilder.CreateSession();
        TestDataBuilder.AddCompositeAllTrueFalse(
            session,
            defaultPoint: 2.0m,
            parts:
            [
                ("True", "True"),   // correct
                ("False", "True"),  // wrong
                ("True", "False"),  // wrong
                ("False", "True"),  // wrong
            ]);

        // Act
        var result = _engine.Grade(session);

        // Assert
        var answer = session.TestAnswers.First();
        Assert.False(answer.IsCorrect); // Not all correct
        Assert.Equal(0.20m, answer.PointsEarned); // 0.10 × 2.0 = 0.20
    }

    [Fact]
    public void CompositeAllTF_2ofN_Correct_PointsEarned_0_25_Times_DefaultPoint()
    {
        // BR-23: 2 correct → 0.25 × dp
        var session = TestDataBuilder.CreateSession();
        TestDataBuilder.AddCompositeAllTrueFalse(
            session,
            defaultPoint: 2.0m,
            parts:
            [
                ("True", "True"),   // correct
                ("False", "False"), // correct
                ("True", "False"),  // wrong
                ("False", "True"),  // wrong
            ]);

        // Act
        var result = _engine.Grade(session);

        // Assert
        var answer = session.TestAnswers.First();
        Assert.False(answer.IsCorrect);
        Assert.Equal(0.50m, answer.PointsEarned); // 0.25 × 2.0 = 0.50
    }

    [Fact]
    public void CompositeAllTF_3ofN_Correct_PointsEarned_0_50_Times_DefaultPoint()
    {
        // BR-23: 3 correct → 0.50 × dp
        var session = TestDataBuilder.CreateSession();
        TestDataBuilder.AddCompositeAllTrueFalse(
            session,
            defaultPoint: 2.0m,
            parts:
            [
                ("True", "True"),    // correct
                ("False", "False"),  // correct
                ("True", "True"),    // correct
                ("False", "True"),   // wrong
            ]);

        // Act
        var result = _engine.Grade(session);

        // Assert
        var answer = session.TestAnswers.First();
        Assert.False(answer.IsCorrect);
        Assert.Equal(1.00m, answer.PointsEarned); // 0.50 × 2.0 = 1.00
    }

    [Fact]
    public void CompositeAllTF_NofN_Correct_PointsEarned_DefaultPoint_And_IsCorrectTrue()
    {
        // BR-23: N (all) correct → 1.00 × dp, is_correct = true
        var session = TestDataBuilder.CreateSession();
        TestDataBuilder.AddCompositeAllTrueFalse(
            session,
            defaultPoint: 2.0m,
            parts:
            [
                ("True", "True"),    // correct
                ("False", "False"),  // correct
                ("True", "True"),    // correct
                ("False", "False"),  // correct
            ]);

        // Act
        var result = _engine.Grade(session);

        // Assert
        var answer = session.TestAnswers.First();
        Assert.True(answer.IsCorrect);
        Assert.Equal(2.0m, answer.PointsEarned); // 1.00 × 2.0 = 2.0
    }

    [Fact]
    public void CompositeAllTF_ChildPartsHavePointsEarnedZero()
    {
        // BR-23 spec: TestAnswerPart.points_earned = 0 — parent is source of truth
        var session = TestDataBuilder.CreateSession();
        TestDataBuilder.AddCompositeAllTrueFalse(
            session,
            defaultPoint: 2.0m,
            parts:
            [
                ("True", "True"),
                ("False", "False"),
            ]);

        // Act
        _engine.Grade(session);

        // Assert
        var answer = session.TestAnswers.First();
        foreach (var part in answer.AnswerParts)
        {
            Assert.Equal(0m, part.PointsEarned); // All child parts = 0
        }
    }

    [Fact]
    public void CompositeAllTF_ChildParts_IsCorrect_RecordedIndividually()
    {
        // BR-23: each TestAnswerPart.is_correct still recorded individually for solution display
        var session = TestDataBuilder.CreateSession();
        TestDataBuilder.AddCompositeAllTrueFalse(
            session,
            defaultPoint: 2.0m,
            parts:
            [
                ("True", "True"),    // correct
                ("False", "True"),   // wrong
                ("True", "True"),    // correct
                ("False", "False"),  // correct
            ]);

        // Act
        _engine.Grade(session);

        // Assert
        var answerParts = session.TestAnswers.First().AnswerParts.OrderBy(p => p.QuestionPart.PartOrder).ToList();
        Assert.True(answerParts[0].IsCorrect);    // True == True
        Assert.False(answerParts[1].IsCorrect);   // False != True
        Assert.True(answerParts[2].IsCorrect);    // True == True
        Assert.True(answerParts[3].IsCorrect);    // False == False
    }

    // ── COMPOSITE GENERAL (MIXED PARTS) ───────────────────────────────────

    [Fact]
    public void CompositeGeneral_ParentScore_EqualsSumOfPartPointsEarned()
    {
        // Arrange: 3 parts with mixed types, student gets 2 out of 3 correct
        var session = TestDataBuilder.CreateSession();
        TestDataBuilder.AddCompositeGeneral(
            session,
            defaultPoint: 3.0m,
            parts:
            [
                ("SHORT_ANSWER", "42", 1.0m, "42"),        // correct → 1.0
                ("TRUE_FALSE", "True", 1.0m, "False"),     // wrong → 0.0
                ("NUMERIC_ANSWER", "3.14", 1.0m, "3.14"),  // correct → 1.0
            ]);

        // Act
        var result = _engine.Grade(session);

        // Assert
        var answer = session.TestAnswers.First();
        Assert.Equal(2.0m, answer.PointsEarned); // 1.0 + 0.0 + 1.0 = 2.0
        Assert.False(answer.IsCorrect); // Not all parts correct
    }

    [Fact]
    public void CompositeGeneral_AllPartsCorrect_IsCorrectTrue()
    {
        // Arrange: all parts correct
        var session = TestDataBuilder.CreateSession();
        TestDataBuilder.AddCompositeGeneral(
            session,
            defaultPoint: 4.0m,
            parts:
            [
                ("SHORT_ANSWER", "A", 2.0m, "a"),       // correct (case insensitive)
                ("TRUE_FALSE", "True", 2.0m, "True"),    // correct
            ]);

        // Act
        _engine.Grade(session);

        // Assert
        var answer = session.TestAnswers.First();
        Assert.True(answer.IsCorrect);
        Assert.Equal(4.0m, answer.PointsEarned);
    }

    [Fact]
    public void CompositeGeneral_PartPointsCappedAtDefaultPoint()
    {
        // Arrange: part point values sum to more than default_point
        var session = TestDataBuilder.CreateSession();
        TestDataBuilder.AddCompositeGeneral(
            session,
            defaultPoint: 2.0m,
            parts:
            [
                ("SHORT_ANSWER", "A", 1.5m, "A"),    // correct → 1.5
                ("SHORT_ANSWER", "B", 1.5m, "B"),    // correct → 1.5
            ]);

        // Act
        _engine.Grade(session);

        // Assert
        var answer = session.TestAnswers.First();
        Assert.Equal(2.0m, answer.PointsEarned); // Capped at DefaultPoint (2.0), not 3.0
    }

    // ── SCORE CALCULATION (BR-20) ─────────────────────────────────────────

    [Fact]
    public void Score_IsSumPointsEarned_DivBySumMaxPoints_Times10()
    {
        // Arrange: 2 questions, one correct (2pt), one incorrect (3pt)
        var correctId = Guid.NewGuid();
        var wrongId = Guid.NewGuid();
        var session = TestDataBuilder.CreateSession();
        TestDataBuilder.AddSingleChoiceAnswer(session, defaultPoint: 2.0m, correctId, studentAnswerId: correctId);
        TestDataBuilder.AddSingleChoiceAnswer(session, defaultPoint: 3.0m, Guid.NewGuid(), studentAnswerId: wrongId);

        // Act
        var result = _engine.Grade(session);

        // Assert: 2.0 / 5.0 × 10.0 = 4.00
        Assert.Equal(4.00m, result.Score);
    }

    // ── TRUE_FALSE (standalone) ───────────────────────────────────────────

    [Fact]
    public void TrueFalse_Correct_SameAsSingleChoice()
    {
        // TRUE_FALSE standalone uses the same path as SINGLE_CHOICE
        var correctId = Guid.NewGuid();
        var session = TestDataBuilder.CreateSession();
        TestDataBuilder.AddSingleChoiceAnswer(
            session, defaultPoint: 1.0m, correctId, studentAnswerId: correctId,
            questionType: "TRUE_FALSE");

        // Act
        var result = _engine.Grade(session);

        // Assert
        Assert.True(session.TestAnswers.First().IsCorrect);
        Assert.Equal(1.0m, session.TestAnswers.First().PointsEarned);
    }

    // ── PERFORMANCE ──────────────────────────────────────────────────────

    [Fact]
    public void Practice_40Questions_GradedInUnder2Seconds()
    {
        // Arrange: create a 40-question Practice session with mixed question types
        var session = TestDataBuilder.CreateSession(testFormat: "Practice");

        for (int i = 0; i < 10; i++)
        {
            var correctId = Guid.NewGuid();
            TestDataBuilder.AddSingleChoiceAnswer(session, defaultPoint: 1.0m, correctId, studentAnswerId: correctId);
        }

        for (int i = 0; i < 10; i++)
        {
            var id1 = Guid.NewGuid();
            var id2 = Guid.NewGuid();
            TestDataBuilder.AddMultipleSelectAnswer(session, defaultPoint: 2.0m, [id1, id2], [id1, id2]);
        }

        for (int i = 0; i < 10; i++)
        {
            TestDataBuilder.AddShortAnswer(session, defaultPoint: 1.5m, "answer", "answer");
        }

        for (int i = 0; i < 10; i++)
        {
            TestDataBuilder.AddCompositeAllTrueFalse(
                session, defaultPoint: 2.0m,
                [("True", "True"), ("False", "False"), ("True", "True"), ("False", "False")]);
        }

        // Act + Assert: should complete in under 2 seconds
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var result = _engine.Grade(session);
        sw.Stop();

        Assert.True(sw.ElapsedMilliseconds < 2000,
            $"Practice grading took {sw.ElapsedMilliseconds}ms, expected < 2000ms");
        Assert.Equal(40, result.NumCorrect + result.NumIncorrect + result.NumAbandoned);
    }
}
