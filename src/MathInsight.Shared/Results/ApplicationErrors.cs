namespace MathInsight.Shared.Results;

public static class ApplicationErrors
{
    public static readonly Error AuthInvalidToken = new(
        "AUTH_INVALID_TOKEN",
        "Invalid or missing account id.");
}
