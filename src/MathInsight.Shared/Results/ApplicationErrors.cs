namespace MathInsight.Shared.Results;

public static class ApplicationErrors
{
    public static readonly Error RequestInvalid = new(
        "REQUEST_INVALID",
        "The request payload is invalid or malformed.");

    public static readonly Error AuthInvalidToken = new(
        "AUTH_INVALID_TOKEN",
        "Invalid or missing account id.");

    public static readonly Error RateLimitExceeded = new(
        "RATE_LIMIT_EXCEEDED",
        "Too many requests. Please try again later.");
}
