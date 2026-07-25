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
}
