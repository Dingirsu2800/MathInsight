using MathInsight.Shared.Results;

namespace MathInsight.Modules.QuestionBank.Errors;

public static class QuestionBankErrors
{
    public static readonly Error QuestionRequestInvalid = new(
        "QUESTION_REQUEST_INVALID",
        "Request body is required.");

    public static readonly Error QuestionIdRequired = new(
        "QUESTION_ID_REQUIRED",
        "Question id is required.");

    public static readonly Error QuestionNotFound = new(
        "QUESTION_NOT_FOUND",
        "Question was not found.");

    public static readonly Error QuestionContentRequired = new(
        "QUESTION_CONTENT_REQUIRED",
        "Question content is required.");

    public static readonly Error QuestionInvalidType = new(
        "QUESTION_INVALID_TYPE",
        "Invalid question type.");

    public static readonly Error QuestionStatusInvalid = new(
        "QUESTION_STATUS_INVALID",
        "Invalid question status.");

    public static readonly Error QuestionDifficultyRequired = new(
        "QUESTION_DIFFICULTY_REQUIRED",
        "Difficulty is required.");

    public static readonly Error QuestionGradeInvalid = new(
        "QUESTION_GRADE_INVALID",
        "Question grade must be 10, 11, or 12.");

    public static readonly Error QuestionDefaultPointInvalid = new(
        "QUESTION_DEFAULT_POINT_INVALID",
        "Question default point must be between 0 and 10.");

    public static readonly Error QuestionTopicRequired = new(
        "QUESTION_TOPIC_REQUIRED",
        "At least one topic is required.");

    public static readonly Error QuestionPrimaryTopicRequired = new(
        "QUESTION_PRIMARY_TOPIC_REQUIRED",
        "Primary topic is required.");

    public static readonly Error QuestionPrimaryTopicInvalid = new(
        "QUESTION_PRIMARY_TOPIC_INVALID",
        "Question requires exactly one primary topic.");

    public static readonly Error QuestionTopicDuplicate = new(
        "QUESTION_TOPIC_DUPLICATE",
        "Question topics must not contain duplicate tags.");

    public static readonly Error QuestionAnswerRequired = new(
        "QUESTION_ANSWER_REQUIRED",
        "Question answers are required.");

    public static readonly Error QuestionAnswerContentRequired = new(
        "QUESTION_ANSWER_CONTENT_REQUIRED",
        "Answer content is required.");

    public static readonly Error QuestionSingleChoiceCorrectAnswerRequired = new(
        "QUESTION_CORRECT_ANSWER_REQUIRED",
        "Single choice question requires exactly one correct answer.");

    public static readonly Error QuestionMultipleChoiceCorrectAnswerRequired = new(
        "QUESTION_CORRECT_ANSWER_REQUIRED",
        "Multiple choice question requires at least one correct answer.");

    public static readonly Error QuestionPartRequired = new(
        "QUESTION_PART_REQUIRED",
        "Composite question requires parts.");

    public static readonly Error QuestionPartContentRequired = new(
        "QUESTION_PART_CONTENT_REQUIRED",
        "Question part content is required.");

    public static readonly Error QuestionPartOrderInvalid = new(
        "QUESTION_PART_ORDER_INVALID",
        "Question part order must be greater than 0.");

    public static readonly Error QuestionPartOrderDuplicate = new(
        "QUESTION_PART_ORDER_DUPLICATE",
        "Question parts must not contain duplicate part orders.");

    public static readonly Error QuestionPartDefaultPointInvalid = new(
        "QUESTION_PART_DEFAULT_POINT_INVALID",
        "Question part default point must be between 0 and 10.");

    public static readonly Error QuestionPartNumericToleranceInvalid = new(
        "QUESTION_PART_NUMERIC_TOLERANCE_INVALID",
        "Question part numeric tolerance must be greater than or equal to 0.");

    public static readonly Error QuestionPartInvalidType = new(
        "QUESTION_PART_INVALID_TYPE",
        "Invalid question part type.");

    public static readonly Error QuestionTrueFalsePartAnswerInvalid = new(
        "QUESTION_PART_ANSWER_INVALID",
        "True/false part requires boolean answer.");

    public static readonly Error QuestionShortAnswerPartAnswerInvalid = new(
        "QUESTION_PART_ANSWER_INVALID",
        "Short answer part requires text answer.");

    public static readonly Error QuestionNumericAnswerPartAnswerInvalid = new(
        "QUESTION_PART_ANSWER_INVALID",
        "Numeric answer part requires numeric answer.");

    public static readonly Error QuestionAnswerNotAllowed = new(
        "QUESTION_ANSWER_NOT_ALLOWED",
        "Composite question must not contain top-level answers.");

    public static readonly Error QuestionPartNotAllowed = new(
        "QUESTION_PART_NOT_ALLOWED",
        "Non-composite question must not contain parts.");

    public static readonly Error QuestionTrueFalseAnswerCountInvalid = new(
        "QUESTION_TRUE_FALSE_ANSWER_COUNT_INVALID",
        "True/false question requires exactly two answers.");

    public static readonly Error QuestionTrueFalseCorrectAnswerRequired = new(
        "QUESTION_TRUE_FALSE_CORRECT_ANSWER_REQUIRED",
        "True/false question requires exactly one correct answer.");

    public static readonly Error QuestionShortAnswerCorrectAnswerRequired = new(
        "QUESTION_SHORT_ANSWER_CORRECT_ANSWER_REQUIRED",
        "Short answer question requires exactly one correct answer.");
}
