using MathInsight.Shared.Results;

namespace MathInsight.Modules.Testing.Errors;

public static class TestingErrors
{
    public static readonly Error TestNotFound = new(
        "TESTING_TEST_NOT_FOUND",
        "The requested test was not found.");

    public static readonly Error TestNotActive = new(
        "TESTING_TEST_NOT_ACTIVE",
        "The test is not in ACTIVE status and cannot be started.");

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
}
