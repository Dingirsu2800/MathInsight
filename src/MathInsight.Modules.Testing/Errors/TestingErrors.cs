using MathInsight.Shared.Results;

namespace MathInsight.Modules.Testing.Errors;

public static class TestingErrors
{
    public static readonly Error TestNotFound = new(
        "TESTING_TEST_NOT_FOUND",
        "The requested test was not found.");

    public static readonly Error TestNotActive = new(
        "TESTING_TEST_NOT_ACTIVE",
        "The test is not in Active status and cannot be started.");

    public static readonly Error TestContainsInvalidatedQuestion = new(
        "TESTING_TEST_CONTAINS_INVALIDATED_QUESTION",
        "This test contains a question whose score was invalidated and cannot accept a new session.");

    public static readonly Error TestAccessDenied = new(
        "TESTING_TEST_ACCESS_DENIED",
        "The student is not allowed to start this test.");

    public static readonly Error SessionNotFound = new(
        "TESTING_SESSION_NOT_FOUND",
        "The requested test session was not found.");

    public static readonly Error SessionNotInProgress = new(
        "TESTING_SESSION_NOT_IN_PROGRESS",
        "The session is not in InProgress status.");

    public static readonly Error SessionAlreadyInProgress = new(
        "TESTING_SESSION_ALREADY_IN_PROGRESS",
        "An InProgress session already exists for this student and test (BR-15).");

    public static readonly Error SessionAlreadyCompleted = new(
        "TESTING_SESSION_ALREADY_COMPLETED",
        "The session has already been graded or abandoned (DC-03).");

    public static readonly Error SessionNotExpired = new(
        "TESTING_SESSION_NOT_EXPIRED",
        "The session duration has not expired.");

    public static readonly Error TestHasNoTimeLimit = new(
        "TESTING_TEST_HAS_NO_TIME_LIMIT",
        "The test has no server-enforced time limit.");

    public static readonly Error SessionExpired = new(
        "TESTING_SESSION_EXPIRED",
        "The session duration has expired and no longer accepts answer changes.");

    public static readonly Error SessionNotGraded = new(
        "TESTING_SESSION_NOT_GRADED",
        "The session must be in Graded status to view solutions.");

    public static readonly Error InvalidIncidentType = new(
        "TESTING_INVALID_INCIDENT_TYPE",
        "Incident type must be TAB_SWITCH or FOCUS_LOSS.");

    public static readonly Error RequestInvalid = new(
        "TESTING_REQUEST_INVALID",
        "The request payload is invalid or malformed.");

    public static readonly Error AnswerNotInVersion = new(
        "ANSWER_NOT_IN_TEST_VERSION",
        "An answer or part does not belong to the question version used by this test.");

    public static readonly Error ShortAnswerNumericRequired = new(
        "ANSWER_SHORT_ANSWER_NUMERIC_REQUIRED",
        "Short answer values must use fixed-point numeric format.");
}
