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
            SessionId = Guid.NewGuid(),
            TestId = Guid.NewGuid(),
            StudentId = Guid.NewGuid(),
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
        Guid correctAnswerId,
        Guid? studentAnswerId,
        string questionType = "SINGLE_CHOICE",
        byte difficultyLevel = 1,
        Guid? primaryTagId = null)
    {
        var questionId = Guid.NewGuid();
        var question = new Question
        {
            QuestionId = questionId,
            QuestionType = questionType,
            DefaultPoint = defaultPoint,
            DifficultyLevel = difficultyLevel,
            QuestionContent = "Test question",
            Answers = new List<Answer>
            {
                new() { AnswerId = correctAnswerId, QuestionId = questionId, AnswerContent = "Correct", IsCorrect = true },
                new() { AnswerId = Guid.NewGuid(), QuestionId = questionId, AnswerContent = "Wrong1", IsCorrect = false },
                new() { AnswerId = Guid.NewGuid(), QuestionId = questionId, AnswerContent = "Wrong2", IsCorrect = false },
            },
            Parts = new List<QuestionPart>(),
            QuestionTopics = primaryTagId.HasValue
                ? new List<QuestionTopic>
                {
                    new() { QuestionTopicId = Guid.NewGuid(), QuestionId = questionId, TagId = primaryTagId.Value, IsPrimary = true }
                }
                : new List<QuestionTopic>()
        };

        var answer = new TestAnswer
        {
            TestAnswerId = Guid.NewGuid(),
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
        List<Guid> correctAnswerIds,
        List<Guid> selectedAnswerIds,
        byte difficultyLevel = 1,
        Guid? primaryTagId = null)
    {
        var questionId = Guid.NewGuid();
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
            var wrongId = Guid.NewGuid();
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
            DefaultPoint = defaultPoint,
            DifficultyLevel = difficultyLevel,
            QuestionContent = "Test multiple select",
            Answers = answers,
            Parts = new List<QuestionPart>(),
            QuestionTopics = primaryTagId.HasValue
                ? new List<QuestionTopic>
                {
                    new() { QuestionTopicId = Guid.NewGuid(), QuestionId = questionId, TagId = primaryTagId.Value, IsPrimary = true }
                }
                : new List<QuestionTopic>()
        };

        var selectedOptions = selectedAnswerIds.Select(id => new TestAnswerOption
        {
            TestAnswerId = Guid.NewGuid(),
            AnswerId = id,
            Answer = answers.FirstOrDefault(a => a.AnswerId == id)!
        }).ToList();

        var testAnswer = new TestAnswer
        {
            TestAnswerId = Guid.NewGuid(),
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
        Guid? primaryTagId = null)
    {
        var questionId = Guid.NewGuid();
        var question = new Question
        {
            QuestionId = questionId,
            QuestionType = "SHORT_ANSWER",
            DefaultPoint = defaultPoint,
            DifficultyLevel = difficultyLevel,
            QuestionContent = "Test short answer",
            Answers = new List<Answer>
            {
                new() { AnswerId = Guid.NewGuid(), QuestionId = questionId, AnswerContent = correctAnswer, IsCorrect = true }
            },
            Parts = new List<QuestionPart>(),
            QuestionTopics = primaryTagId.HasValue
                ? new List<QuestionTopic>
                {
                    new() { QuestionTopicId = Guid.NewGuid(), QuestionId = questionId, TagId = primaryTagId.Value, IsPrimary = true }
                }
                : new List<QuestionTopic>()
        };

        var answer = new TestAnswer
        {
            TestAnswerId = Guid.NewGuid(),
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
        Guid? primaryTagId = null)
    {
        var questionId = Guid.NewGuid();
        var questionParts = new List<QuestionPart>();

        for (int i = 0; i < parts.Count; i++)
        {
            questionParts.Add(new QuestionPart
            {
                QuestionPartId = Guid.NewGuid(),
                QuestionId = questionId,
                PartOrder = i + 1,
                Content = $"Part {i + 1}",
                AnswerKey = parts[i].answerKey,
                PointValue = 0, // Not used for all-TF scoring
                PartType = "TRUE_FALSE"
            });
        }

        var question = new Question
        {
            QuestionId = questionId,
            QuestionType = "COMPOSITE",
            DefaultPoint = defaultPoint,
            DifficultyLevel = difficultyLevel,
            QuestionContent = "Test composite",
            Answers = new List<Answer>(),
            Parts = questionParts,
            QuestionTopics = primaryTagId.HasValue
                ? new List<QuestionTopic>
                {
                    new() { QuestionTopicId = Guid.NewGuid(), QuestionId = questionId, TagId = primaryTagId.Value, IsPrimary = true }
                }
                : new List<QuestionTopic>()
        };

        var answerParts = new List<TestAnswerPart>();
        var testAnswerId = Guid.NewGuid();
        for (int i = 0; i < questionParts.Count; i++)
        {
            answerParts.Add(new TestAnswerPart
            {
                TestAnswerPartId = Guid.NewGuid(),
                TestAnswerId = testAnswerId,
                QuestionPartId = questionParts[i].QuestionPartId,
                StudentAnswer = parts[i].studentAnswer,
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
        Guid? primaryTagId = null)
    {
        var questionId = Guid.NewGuid();
        var questionParts = new List<QuestionPart>();

        for (int i = 0; i < parts.Count; i++)
        {
            questionParts.Add(new QuestionPart
            {
                QuestionPartId = Guid.NewGuid(),
                QuestionId = questionId,
                PartOrder = i + 1,
                Content = $"Part {i + 1}",
                AnswerKey = parts[i].answerKey,
                PointValue = parts[i].pointValue,
                PartType = parts[i].partType
            });
        }

        var question = new Question
        {
            QuestionId = questionId,
            QuestionType = "COMPOSITE",
            DefaultPoint = defaultPoint,
            DifficultyLevel = difficultyLevel,
            QuestionContent = "Test composite general",
            Answers = new List<Answer>(),
            Parts = questionParts,
            QuestionTopics = primaryTagId.HasValue
                ? new List<QuestionTopic>
                {
                    new() { QuestionTopicId = Guid.NewGuid(), QuestionId = questionId, TagId = primaryTagId.Value, IsPrimary = true }
                }
                : new List<QuestionTopic>()
        };

        var answerParts = new List<TestAnswerPart>();
        var testAnswerId = Guid.NewGuid();
        for (int i = 0; i < questionParts.Count; i++)
        {
            answerParts.Add(new TestAnswerPart
            {
                TestAnswerPartId = Guid.NewGuid(),
                TestAnswerId = testAnswerId,
                QuestionPartId = questionParts[i].QuestionPartId,
                StudentAnswer = parts[i].studentAnswer,
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
