using MathInsight.Shared.Results;

namespace MathInsight.Modules.TestGen.Errors;

public static class TestGenerationErrors
{
    public static readonly Error RequestInvalid = new(
        "TEST_GENERATION_REQUEST_INVALID",
        "Test generation request is invalid.");

    public static readonly Error StudentNotFound = new(
        "TEST_GENERATION_STUDENT_NOT_FOUND",
        "A usable Student profile was not found.");

    public static readonly Error BlueprintNotFound = new(
        "TEST_GENERATION_BLUEPRINT_NOT_FOUND",
        "The requested blueprint was not found.");

    public static readonly Error BlueprintUnavailable = new(
        "TEST_GENERATION_BLUEPRINT_UNAVAILABLE",
        "The blueprint is not available for test generation.");

    public static readonly Error GradeMismatch = new(
        "TEST_GENERATION_GRADE_MISMATCH",
        "The blueprint grade does not match the Student's current grade.");

    public static readonly Error InsufficientQuestions = new(
        "TEST_GENERATION_INSUFFICIENT_QUESTIONS",
        "The approved active question pool cannot fulfill the blueprint.");

    public static readonly Error ScoreBudgetMismatch = new(
        "BLUEPRINT_SCORE_BUDGET_MISMATCH",
        "Blueprint section score budgets do not equal the total score.");

    public static readonly Error QuestionPoolInsufficient = new(
        "QUESTION_POOL_INSUFFICIENT",
        "The valid question pool cannot fulfill every blueprint detail.");

    public static readonly Error QuestionVersionMissing = new(
        "QUESTION_VERSION_MISSING",
        "A latest supported QuestionVersion snapshot is missing or invalid.");

    public static readonly Error GenerationConflict = new(
        "TEST_GENERATION_CONFLICT",
        "Test generation conflicted with another persisted operation.");

    public static readonly Error TestCodeNotAvailable = new(
        "TEST_CODE_NOT_AVAILABLE",
        "The test code is not available.");

    public static readonly Error GeneratedTestNotFound = new(
        "GENERATED_TEST_NOT_FOUND",
        "The generated test was not found or is unavailable.");

    public static readonly Error TopicPracticeStudentNotFound = new("TOPIC_PRACTICE_STUDENT_NOT_FOUND", "A usable Student profile was not found.");
    public static readonly Error TopicPracticeTopicNotFound = new("TOPIC_PRACTICE_TOPIC_NOT_FOUND", "The requested topic was not found.");
    public static readonly Error TopicPracticeTopicUnavailable = new("TOPIC_PRACTICE_TOPIC_UNAVAILABLE", "The requested topic is inactive or does not match the Student grade.");
    public static readonly Error TopicPracticeInsufficientQuestions = new("TOPIC_PRACTICE_INSUFFICIENT_QUESTIONS", "The selected topic does not contain ten valid questions.");
    public static readonly Error TopicPracticeGenerationConflict = new("TOPIC_PRACTICE_GENERATION_CONFLICT", "The generated test could not be verified after a persistence conflict.");
}
