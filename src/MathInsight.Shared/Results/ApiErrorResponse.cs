namespace MathInsight.Shared.Results;

public sealed record ApiErrorResponse(string Code, string Message)
{
    public ApiErrorResponse(Error error)
        : this(error.Code, error.Message)
    {
    }
}
