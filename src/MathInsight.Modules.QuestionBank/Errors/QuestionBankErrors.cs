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

    public static readonly Error QuestionUpdateForbidden = new(
        "QUESTION_UPDATE_FORBIDDEN",
        "You are not allowed to update this question.");

    public static readonly Error QuestionMutationForbidden = new(
        "QUESTION_MUTATION_FORBIDDEN",
        "You are not allowed to modify this question.");

    public static readonly Error QuestionInUse = new(
        "QUESTION_IN_USE",
        "Question is already used in a test and cannot be deactivated or hard-deleted.");

    public static readonly Error QuestionHasPendingReports = new(
        "QUESTION_HAS_PENDING_REPORTS",
        "Question has pending reports or is currently reported and cannot be hard-deleted.");

    public static readonly Error QuestionContentRequired = new(
        "QUESTION_CONTENT_REQUIRED",
        "Question content is required.");

    public static readonly Error ImageRequired = new(
        "IMAGE_REQUIRED",
        "An image file is required.");

    public static readonly Error ImageTypeNotSupported = new(
        "IMAGE_TYPE_NOT_SUPPORTED",
        "Only JPEG, PNG, and WebP images are supported.");

    public static readonly Error ImageTooLarge = new(
        "IMAGE_TOO_LARGE",
        "Image size must not exceed 5 MB.");

    public static readonly Error ImageStorageUnavailable = new(
        "IMAGE_STORAGE_UNAVAILABLE",
        "Image storage is not configured.");

    public static readonly Error ImageUploadFailed = new(
        "IMAGE_UPLOAD_FAILED",
        "Image upload failed.");

    public static readonly Error OcrNotConfigured = new(
        "OCR_NOT_CONFIGURED",
        "OCR service is not configured.");

    public static readonly Error OcrProviderUnavailable = new(
        "OCR_PROVIDER_UNAVAILABLE",
        "OCR provider is currently unavailable.");

    public static readonly Error OcrProviderRateLimited = new(
        "OCR_PROVIDER_RATE_LIMITED",
        "OCR provider is temporarily rate limited. Please try again later.");

    public static readonly Error OcrTimeout = new(
        "OCR_TIMEOUT",
        "OCR request timed out. Please try again.");

    public static readonly Error OcrInvalidResponse = new(
        "OCR_INVALID_RESPONSE",
        "OCR provider returned an invalid response.");

    public static readonly Error OcrDraftUnavailable = new(
        "OCR_DRAFT_UNAVAILABLE",
        "OCR could not extract a usable question draft from this image.");

    public static readonly Error OcrRateLimitExceeded = new(
        "OCR_RATE_LIMIT_EXCEEDED",
        "Too many OCR requests. Please wait before trying again.");

    public static readonly Error QuestionInvalidType = new(
        "QUESTION_INVALID_TYPE",
        "Invalid question type.");

    public static readonly Error QuestionStatusInvalid = new(
        "QUESTION_STATUS_INVALID",
        "Invalid question status.");

    public static readonly Error QuestionDifficultyRequired = new(
        "QUESTION_DIFFICULTY_REQUIRED",
        "Difficulty is required.");

    public static readonly Error QuestionDifficultyNotFound = new(
        "QUESTION_DIFFICULTY_NOT_FOUND",
        "Question difficulty was not found.");

    public static readonly Error QuestionGradeInvalid = new(
        "QUESTION_GRADE_INVALID",
        "Question grade must be 10, 11, or 12.");

    public static readonly Error QuestionDefaultPointInvalid = new(
        "QUESTION_DEFAULT_POINT_INVALID",
        "Question default point must be between 0 and 10.");

    public static readonly Error QuestionTopicRequired = new(
        "QUESTION_TOPIC_REQUIRED",
        "At least one topic is required.");

    public static readonly Error QuestionTopicNotFound = new(
        "QUESTION_TOPIC_NOT_FOUND",
        "Question topic was not found.");

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

    public static readonly Error QuestionPartLabelInvalid = new(
        "QUESTION_PART_LABEL_INVALID",
        "Question part label must not exceed 10 characters.");

    public static readonly Error QuestionPartLabelDuplicate = new(
        "QUESTION_PART_LABEL_DUPLICATE",
        "Question parts must not contain duplicate labels.");

    public static readonly Error QuestionPartDefaultPointInvalid = new(
        "QUESTION_PART_DEFAULT_POINT_INVALID",
        "Question part default point must be between 0 and 10.");

    public static readonly Error QuestionPartNumericToleranceInvalid = new(
        "QUESTION_PART_NUMERIC_TOLERANCE_INVALID",
        "Question part numeric tolerance must be greater than or equal to 0.");

    public static readonly Error QuestionPartNumericValueInvalid = new(
        "QUESTION_PART_NUMERIC_VALUE_INVALID",
        "Question part numeric values must fit decimal(18,6).");

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

    public static readonly Error TagRequestInvalid = new(
        "TAG_REQUEST_INVALID",
        "Request body is required.");

    public static readonly Error TagIdRequired = new(
        "TAG_ID_REQUIRED",
        "Tag id is required.");

    public static readonly Error TagNameRequired = new(
        "TAG_NAME_REQUIRED",
        "Tag name is required.");

    public static readonly Error TagNameTooLong = new(
        "TAG_NAME_TOO_LONG",
        "Tag name must not exceed 50 characters.");

    public static readonly Error TagDescriptionTooLong = new(
        "TAG_DESCRIPTION_TOO_LONG",
        "Tag description must not exceed 255 characters.");

    public static readonly Error TagGradeInvalid = new(
        "TAG_GRADE_INVALID",
        "Tag grade must be 10, 11, or 12.");

    public static readonly Error TagDisplayOrderInvalid = new(
        "TAG_DISPLAY_ORDER_INVALID",
        "Tag display order must be greater than 0.");

    public static readonly Error TagTopicNotFound = new(
        "TAG_TOPIC_NOT_FOUND",
        "Topic tag was not found.");

    public static readonly Error TagDifficultyNotFound = new(
        "TAG_DIFFICULTY_NOT_FOUND",
        "Difficulty tag was not found.");

    public static readonly Error TagParentNotFound = new(
        "TAG_PARENT_NOT_FOUND",
        "Parent topic tag was not found.");

    public static readonly Error TagParentInvalid = new(
        "TAG_PARENT_INVALID",
        "Parent topic tag is invalid.");

    public static readonly Error TagStructureImmutable = new(
        "TAG_STRUCTURE_IMMUTABLE",
        "Topic grade and parent cannot be changed after creation.");

    public static readonly Error TagNameDuplicate = new(
        "TAG_NAME_DUPLICATE",
        "Tag name already exists.");

    public static readonly Error TagLevelValueInvalid = new(
        "TAG_LEVEL_VALUE_INVALID",
        "Difficulty level value must be greater than 0.");

    public static readonly Error TagLevelValueImmutable = new(
        "TAG_LEVEL_VALUE_IMMUTABLE",
        "Difficulty level value cannot be changed after creation.");

    public static readonly Error TagLevelValueDuplicate = new(
        "TAG_LEVEL_VALUE_DUPLICATE",
        "Difficulty level value already exists.");

    public static readonly Error TagTopicHasActiveDescendants = new(
        "TAG_TOPIC_HAS_ACTIVE_DESCENDANTS",
        "Topic has active descendant topics and cannot be deactivated.");

    public static readonly Error ReportReasonRequired = new(
        "REPORT_REASON_REQUIRED",
        "Report reason is required.");

    public static readonly Error ReportReasonTooLong = new(
        "REPORT_REASON_TOO_LONG",
        "Report reason must not exceed 2000 characters.");

    public static readonly Error ReportStatusInvalid = new(
        "REPORT_STATUS_INVALID",
        "Invalid report status.");

    public static readonly Error QuestionSelfReportForbidden = new(
        "QUESTION_SELF_REPORT_FORBIDDEN",
        "Experts cannot report their own questions.");

    public static readonly Error ReportAccessForbidden = new(
        "REPORT_ACCESS_FORBIDDEN",
        "You are not allowed to access or handle this report.");

    public static readonly Error ReportNotFound = new(
        "REPORT_NOT_FOUND",
        "Question report was not found.");

    public static readonly Error ReportAlreadyPending = new(
        "REPORT_ALREADY_PENDING",
        "You already have a pending report for this question.");

    public static readonly Error ReportAlreadyHandled = new(
        "REPORT_ALREADY_HANDLED",
        "Question report has already been handled.");

    public static readonly Error AdminReportWorkflowAlreadyExists = new(
        "ADMIN_REPORT_WORKFLOW_ALREADY_EXISTS",
        "An active Admin report workflow already exists for this question.");

    public static readonly Error AdminReportRequiresReview = new(
        "ADMIN_REPORT_REQUIRES_REVIEW",
        "An Admin report must be submitted and reviewed through the Admin workflow.");

    public static readonly Error ReviewNoteRequired = new(
        "REVIEW_NOTE_REQUIRED",
        "Review note is required.");

    public static readonly Error ReviewNoteTooLong = new(
        "REVIEW_NOTE_TOO_LONG",
        "Review note must not exceed 2000 characters.");

    public static readonly Error QuestionNotReportable = new(
        "QUESTION_NOT_REPORTABLE",
        "Question cannot be reported in its current state.");

    public static readonly Error QuestionImportFileRequired = new(
        "QUESTION_IMPORT_FILE_REQUIRED",
        "An Excel import file is required.");

    public static readonly Error QuestionImportFileTooLarge = new(
        "QUESTION_IMPORT_FILE_TOO_LARGE",
        "Excel import file size must not exceed 20 MB.");

    public static readonly Error QuestionImportFileTypeNotSupported = new(
        "QUESTION_IMPORT_FILE_TYPE_NOT_SUPPORTED",
        "Only .xlsx Excel files are supported for question import.");

    public static readonly Error QuestionImportTemplateInvalid = new(
        "QUESTION_IMPORT_TEMPLATE_INVALID",
        "Excel file does not match the MathInsight import template.");

    public static readonly Error QuestionImportTemplateVersionUnsupported = new(
        "QUESTION_IMPORT_TEMPLATE_VERSION_UNSUPPORTED",
        "Excel import template version is not supported.");

    public static readonly Error QuestionImportLimitExceeded = new(
        "QUESTION_IMPORT_LIMIT_EXCEEDED",
        "Excel import exceeds the supported batch limit.");

    public static readonly Error QuestionImportValidationFailed = new(
        "QUESTION_IMPORT_VALIDATION_FAILED",
        "One or more imported questions are invalid.");

    public static readonly Error QuestionImportNoQuestions = new(
        "QUESTION_IMPORT_NO_QUESTIONS",
        "Excel import must contain at least one question row.");

    public static readonly Error QuestionImportIdInvalid = new(
        "QUESTION_IMPORT_ID_INVALID",
        "ImportId is required.");

    public static readonly Error QuestionImportFormulaNotAllowed = new(
        "QUESTION_IMPORT_FORMULA_NOT_ALLOWED",
        "Formula cells are not supported in import data.");

    public static readonly Error QuestionImportQuestionKeyInvalid = new(
        "QUESTION_IMPORT_QUESTION_KEY_INVALID",
        "QuestionKey is required and must not exceed 50 characters.");

    public static readonly Error QuestionImportQuestionKeyDuplicate = new(
        "QUESTION_IMPORT_QUESTION_KEY_DUPLICATE",
        "QuestionKey must be unique in the workbook.");

    public static readonly Error QuestionImportOrphanRow = new(
        "QUESTION_IMPORT_ORPHAN_ROW",
        "A child row references a QuestionKey that does not exist in Questions.");

    public static readonly Error QuestionImportNumericInvalid = new(
        "QUESTION_IMPORT_NUMERIC_INVALID",
        "Value must be a valid number without thousands separators.");

    public static readonly Error QuestionImportBooleanInvalid = new(
        "QUESTION_IMPORT_BOOLEAN_INVALID",
        "Value must be true or false.");

    public static readonly Error QuestionImportTopicAmbiguous = new(
        "QUESTION_IMPORT_TOPIC_AMBIGUOUS",
        "Topic name matches more than one active topic in the question grade.");
}
