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

    public static readonly Error SharedExamGenerationTypeInvalid = new(
        "SHARED_EXAM_GENERATION_TYPE_INVALID",
        "The shared exam has unsupported or mixed generation metadata.");

    public static readonly Error GeneratedTestNotFound = new(
        "GENERATED_TEST_NOT_FOUND",
        "The generated test was not found or is unavailable.");

    public static readonly Error FixedTestBlueprintNotApproved = new("FIXED_TEST_BLUEPRINT_NOT_APPROVED", "The blueprint must be approved or active before a fixed test can be created.");
    public static readonly Error FixedTestQuestionDuplicated = new("FIXED_TEST_QUESTION_DUPLICATED", "A question can only appear once in a fixed test.");
    public static readonly Error FixedTestOrderInvalid = new("FIXED_TEST_ORDER_INVALID", "Question order must be unique and continuous from one.");
    public static readonly Error FixedTestDetailQuantityMismatch = new("FIXED_TEST_DETAIL_QUANTITY_MISMATCH", "Selected question quantities do not fulfill every blueprint detail.");
    public static readonly Error FixedTestQuestionNotEligible = new("FIXED_TEST_QUESTION_NOT_ELIGIBLE", "A selected question does not satisfy its assigned blueprint detail.");
    public static readonly Error FixedTestQuestionVersionUnavailable = new("FIXED_TEST_QUESTION_VERSION_UNAVAILABLE", "A selected question does not have a usable latest version snapshot.");
    public static readonly Error TestAlreadyArchived = new("TEST_ALREADY_ARCHIVED", "The test has already been archived.");

    public static readonly Error TopicPracticeStudentNotFound = new("TOPIC_PRACTICE_STUDENT_NOT_FOUND", "A usable Student profile was not found.");
    public static readonly Error StudentGradeRequired = new("STUDENT_GRADE_REQUIRED", "The Student profile must have a valid current grade before Topic Practice can be generated.");
    public static readonly Error TopicPracticeTopicNotFound = new("TOPIC_PRACTICE_TOPIC_NOT_FOUND", "The requested topic was not found.");
    public static readonly Error TopicPracticeTopicUnavailable = new("TOPIC_PRACTICE_TOPIC_UNAVAILABLE", "The requested topic is inactive or does not match the Student grade.");
    public static readonly Error TopicPracticeDifficultyNotFound = new("TOPIC_PRACTICE_DIFFICULTY_NOT_FOUND", "The selected difficulty was not found.");
    public static readonly Error TopicPracticeDifficultyUnavailable = new("TOPIC_PRACTICE_DIFFICULTY_UNAVAILABLE", "The selected difficulty is inactive or cannot be used for Topic Practice.");
    public static readonly Error TopicPracticeGradeNotAllowed = new("TOPIC_PRACTICE_GRADE_NOT_ALLOWED", "The selected topic belongs to a grade above the Student's current grade.");
    public static readonly Error TopicParentGradeMismatch = new("TOPIC_PARENT_GRADE_MISMATCH", "The selected topic and its parent must belong to the same grade.");
    public static readonly Error TopicParentNotAssignable = new("TOPIC_PARENT_NOT_ASSIGNABLE", "Only an active direct child of an active root topic can be selected for Topic Practice.");
    public static readonly Error TopicPracticeInsufficientQuestions = new("TOPIC_PRACTICE_INSUFFICIENT_QUESTIONS", "The selected topic does not contain ten valid questions.");
    public static readonly Error TopicPracticeGenerationConflict = new("TOPIC_PRACTICE_GENERATION_CONFLICT", "The generated test could not be verified after a persistence conflict.");
    public static readonly Error TopicPracticeRecommenderUnavailable = new("TOPIC_PRACTICE_RECOMMENDER_UNAVAILABLE", "Recommendation advice is temporarily unavailable.");
    public static readonly Error TopicPracticeRecommendationInvalid = new("TOPIC_PRACTICE_RECOMMENDATION_INVALID", "Recommendation advice could not be safely applied.");
}
