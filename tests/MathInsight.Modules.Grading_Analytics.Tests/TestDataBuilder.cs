using MathInsight.Modules.Grading_Analytics.Persistence.Entities;

namespace MathInsight.Modules.Grading_Analytics.Tests;

/// <summary>
/// Fluent builder helpers for constructing in-memory entity graphs
/// used by GradingEngine unit tests.
/// </summary>
internal static class TestDataBuilder
{
    /// <summary>
    /// Creates a minimal TestSession with 0 answers.
    /// Use AddSingleChoiceAnswer, AddMultipleSelectAnswer, etc. to populate.
    /// </summary>
    public static TestSession CreateSession(
        string testFormat = "Practice",
        string status = "InProgress")
    {
        return new TestSession
        {
            SessionId = Guid.NewGuid().ToString("D"),
            TestId = Guid.NewGuid().ToString("D"),
            StudentId = Guid.NewGuid().ToString("D"),
            TestFormat = testFormat,
            Status = status,
            TotalQuestion = 0,
            TestAnswers = new List<TestAnswer>()
        };
    }

    /// <summary>
    /// Adds a SINGLE_CHOICE or TRUE_FALSE answer to the session.
    /// </summary>
    public static TestAnswer AddSingleChoiceAnswer(
        TestSession session,
        decimal defaultPoint,
        string correctAnswerId,
        string? studentAnswerId,
        string questionType = "SINGLE_CHOICE",
        byte difficultyLevel = 1,
        string? primaryTagId = null)
    {
        var questionId = Guid.NewGuid().ToString("D");
        var question = new Question
        {
            QuestionId = questionId,
            QuestionType = questionType,
            DefaultWeight = defaultPoint,
            QuestionContent = "Test question",
            Answers = new List<Answer>
            {
                new() { AnswerId = correctAnswerId, QuestionId = questionId, AnswerContent = "Correct", IsCorrect = true },
                new() { AnswerId = Guid.NewGuid().ToString("D"), QuestionId = questionId, AnswerContent = "Wrong1", IsCorrect = false },
                new() { AnswerId = Guid.NewGuid().ToString("D"), QuestionId = questionId, AnswerContent = "Wrong2", IsCorrect = false },
            },
            Parts = new List<QuestionPart>(),
            QuestionTopics = primaryTagId is not null
                ? new List<QuestionTopic>
                {
                    new() { QuestionTopicId = Guid.NewGuid().ToString("D"), QuestionId = questionId, TagId = primaryTagId, IsPrimary = true }
                }
                : new List<QuestionTopic>()
        };

        var answer = new TestAnswer
        {
            TestAnswerId = Guid.NewGuid().ToString("D"),
            SessionId = session.SessionId,
            QuestionId = questionId,
            AnswerId = studentAnswerId,
            QuestionNo = session.TestAnswers.Count + 1,
            Question = question,
            SelectedOptions = new List<TestAnswerOption>(),
            AnswerParts = new List<TestAnswerPart>(),
            Session = session
        };

        session.TestAnswers.Add(answer);
        session.TotalQuestion = session.TestAnswers.Count;
        return answer;
    }

    /// <summary>
    /// Adds a MULTIPLE_SELECT answer to the session.
    /// </summary>
    public static TestAnswer AddMultipleSelectAnswer(
        TestSession session,
        decimal defaultPoint,
        List<string> correctAnswerIds,
        List<string> selectedAnswerIds,
        byte difficultyLevel = 1,
        string? primaryTagId = null)
    {
        var questionId = Guid.NewGuid().ToString("D");
        var answers = new List<Answer>();

        // Create correct answers
        foreach (var id in correctAnswerIds)
        {
            answers.Add(new Answer
            {
                AnswerId = id,
                QuestionId = questionId,
                AnswerContent = $"Answer {id}",
                IsCorrect = true
            });
        }

        // Create some incorrect answers that aren't in correctAnswerIds
        for (int i = 0; i < 2; i++)
        {
            var wrongId = Guid.NewGuid().ToString("D");
            if (!correctAnswerIds.Contains(wrongId))
            {
                answers.Add(new Answer
                {
                    AnswerId = wrongId,
                    QuestionId = questionId,
                    AnswerContent = $"Wrong {i}",
                    IsCorrect = false
                });
            }
        }

        var question = new Question
        {
            QuestionId = questionId,
            QuestionType = "MULTIPLE_SELECT",
            DefaultWeight = defaultPoint,
            QuestionContent = "Test multiple select",
            Answers = answers,
            Parts = new List<QuestionPart>(),
            QuestionTopics = primaryTagId is not null
                ? new List<QuestionTopic>
                {
                    new() { QuestionTopicId = Guid.NewGuid().ToString("D"), QuestionId = questionId, TagId = primaryTagId, IsPrimary = true }
                }
                : new List<QuestionTopic>()
        };

        var selectedOptions = selectedAnswerIds.Select(id => new TestAnswerOption
        {
            TestAnswerId = Guid.NewGuid().ToString("D"),
            AnswerId = id,
            Answer = answers.FirstOrDefault(a => a.AnswerId == id)!
        }).ToList();

        var testAnswer = new TestAnswer
        {
            TestAnswerId = Guid.NewGuid().ToString("D"),
            SessionId = session.SessionId,
            QuestionId = questionId,
            AnswerId = null,
            QuestionNo = session.TestAnswers.Count + 1,
            Question = question,
            SelectedOptions = selectedOptions,
            AnswerParts = new List<TestAnswerPart>(),
            Session = session
        };

        foreach (var opt in selectedOptions)
            opt.TestAnswerId = testAnswer.TestAnswerId;

        session.TestAnswers.Add(testAnswer);
        session.TotalQuestion = session.TestAnswers.Count;
        return testAnswer;
    }

    /// <summary>
    /// Adds a SHORT_ANSWER question to the session.
    /// </summary>
    public static TestAnswer AddShortAnswer(
        TestSession session,
        decimal defaultPoint,
        string correctAnswer,
        string? studentAnswer,
        byte difficultyLevel = 1,
        string? primaryTagId = null)
    {
        var questionId = Guid.NewGuid().ToString("D");
        var question = new Question
        {
            QuestionId = questionId,
            QuestionType = "SHORT_ANSWER",
            DefaultWeight = defaultPoint,
            QuestionContent = "Test short answer",
            Answers = new List<Answer>
            {
                new() { AnswerId = Guid.NewGuid().ToString("D"), QuestionId = questionId, AnswerContent = correctAnswer, IsCorrect = true }
            },
            Parts = new List<QuestionPart>(),
            QuestionTopics = primaryTagId is not null
                ? new List<QuestionTopic>
                {
                    new() { QuestionTopicId = Guid.NewGuid().ToString("D"), QuestionId = questionId, TagId = primaryTagId, IsPrimary = true }
                }
                : new List<QuestionTopic>()
        };

        var answer = new TestAnswer
        {
            TestAnswerId = Guid.NewGuid().ToString("D"),
            SessionId = session.SessionId,
            QuestionId = questionId,
            AnswerId = null,
            QuestionNo = session.TestAnswers.Count + 1,
            ShortAnswerText = studentAnswer,
            Question = question,
            SelectedOptions = new List<TestAnswerOption>(),
            AnswerParts = new List<TestAnswerPart>(),
            Session = session
        };

        session.TestAnswers.Add(answer);
        session.TotalQuestion = session.TestAnswers.Count;
        return answer;
    }

    /// <summary>
    /// Adds a COMPOSITE question with all TRUE_FALSE parts to the session.
    /// </summary>
    public static TestAnswer AddCompositeAllTrueFalse(
        TestSession session,
        decimal defaultPoint,
        List<(string answerKey, string? studentAnswer)> parts,
        byte difficultyLevel = 1,
        string? primaryTagId = null)
    {
        var questionId = Guid.NewGuid().ToString("D");
        var questionParts = new List<QuestionPart>();

        for (int i = 0; i < parts.Count; i++)
        {
            questionParts.Add(new QuestionPart
            {
                QuestionPartId = Guid.NewGuid().ToString("D"),
                QuestionId = questionId,
                PartOrder = i + 1,
                Content = $"Part {i + 1}",
                CorrectBoolean = bool.TryParse(parts[i].answerKey, out var bVal) ? bVal : (bool?)null,
                DefaultWeight = 1m,
                PartType = "TRUE_FALSE"
            });
        }

        var question = new Question
        {
            QuestionId = questionId,
            QuestionType = "COMPOSITE",
            DefaultWeight = defaultPoint,
            QuestionContent = "Test composite",
            Answers = new List<Answer>(),
            Parts = questionParts,
            QuestionTopics = primaryTagId is not null
                ? new List<QuestionTopic>
                {
                    new() { QuestionTopicId = Guid.NewGuid().ToString("D"), QuestionId = questionId, TagId = primaryTagId, IsPrimary = true }
                }
                : new List<QuestionTopic>()
        };

        var answerParts = new List<TestAnswerPart>();
        var testAnswerId = Guid.NewGuid().ToString("D");
        for (int i = 0; i < questionParts.Count; i++)
        {
            answerParts.Add(new TestAnswerPart
            {
                TestAnswerId = testAnswerId,
                PartId = questionParts[i].QuestionPartId,
                BooleanAnswer = bool.TryParse(parts[i].studentAnswer, out var saVal) ? saVal : (bool?)null,
                QuestionPart = questionParts[i]
            });
        }

        var answer = new TestAnswer
        {
            TestAnswerId = testAnswerId,
            SessionId = session.SessionId,
            QuestionId = questionId,
            AnswerId = null,
            QuestionNo = session.TestAnswers.Count + 1,
            Question = question,
            SelectedOptions = new List<TestAnswerOption>(),
            AnswerParts = answerParts,
            Session = session
        };

        foreach (var ap in answerParts)
            ap.TestAnswer = answer;

        session.TestAnswers.Add(answer);
        session.TotalQuestion = session.TestAnswers.Count;
        return answer;
    }

    /// <summary>
    /// Adds a COMPOSITE question with mixed part types (general scoring) to the session.
    /// </summary>
    public static TestAnswer AddCompositeGeneral(
        TestSession session,
        decimal defaultPoint,
        List<(string partType, string answerKey, decimal pointValue, string? studentAnswer)> parts,
        byte difficultyLevel = 1,
        string? primaryTagId = null)
    {
        var questionId = Guid.NewGuid().ToString("D");
        var questionParts = new List<QuestionPart>();

        for (int i = 0; i < parts.Count; i++)
        {
            var pType = parts[i].partType;
            var pTypeNorm = pType.Replace("_", "").Replace(" ", "").ToUpperInvariant();

            questionParts.Add(new QuestionPart
            {
                QuestionPartId = Guid.NewGuid().ToString("D"),
                QuestionId = questionId,
                PartOrder = i + 1,
                Content = $"Part {i + 1}",
                CorrectBoolean = pTypeNorm == "TRUEFALSE" && bool.TryParse(parts[i].answerKey, out var b) ? b : (bool?)null,
                CorrectText = pTypeNorm == "SHORTANSWER" ? parts[i].answerKey : null,
                CorrectNumeric = pTypeNorm == "NUMERICANSWER" && decimal.TryParse(parts[i].answerKey, out var d) ? d : (decimal?)null,
                NumericTolerance = pTypeNorm == "NUMERICANSWER" ? 0.01m : (decimal?)null,
                DefaultWeight = parts[i].pointValue,
                PartType = pType
            });
        }

        var question = new Question
        {
            QuestionId = questionId,
            QuestionType = "COMPOSITE",
            DefaultWeight = defaultPoint,
            QuestionContent = "Test composite general",
            Answers = new List<Answer>(),
            Parts = questionParts,
            QuestionTopics = primaryTagId is not null
                ? new List<QuestionTopic>
                {
                    new() { QuestionTopicId = Guid.NewGuid().ToString("D"), QuestionId = questionId, TagId = primaryTagId, IsPrimary = true }
                }
                : new List<QuestionTopic>()
        };

        var answerParts = new List<TestAnswerPart>();
        var testAnswerId = Guid.NewGuid().ToString("D");
        for (int i = 0; i < questionParts.Count; i++)
        {
            var pType = questionParts[i].PartType;
            var pTypeNorm = pType.Replace("_", "").Replace(" ", "").ToUpperInvariant();

            answerParts.Add(new TestAnswerPart
            {
                TestAnswerId = testAnswerId,
                PartId = questionParts[i].QuestionPartId,
                BooleanAnswer = pTypeNorm == "TRUEFALSE" && bool.TryParse(parts[i].studentAnswer, out var sb) ? sb : (bool?)null,
                TextAnswer = pTypeNorm == "SHORTANSWER" ? parts[i].studentAnswer : null,
                NumericAnswer = pTypeNorm == "NUMERICANSWER" && decimal.TryParse(parts[i].studentAnswer, out var sd) ? sd : (decimal?)null,
                QuestionPart = questionParts[i]
            });
        }

        var answer = new TestAnswer
        {
            TestAnswerId = testAnswerId,
            SessionId = session.SessionId,
            QuestionId = questionId,
            AnswerId = null,
            QuestionNo = session.TestAnswers.Count + 1,
            Question = question,
            SelectedOptions = new List<TestAnswerOption>(),
            AnswerParts = answerParts,
            Session = session
        };

        foreach (var ap in answerParts)
            ap.TestAnswer = answer;

        session.TestAnswers.Add(answer);
        session.TotalQuestion = session.TestAnswers.Count;
        return answer;
    }
}
