using System.Text.Json.Serialization;

namespace MathInsight.Shared.Results;

public sealed record ApiErrorResponse(
    string Code,
    string Message,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] object? Details = null)
{
    public ApiErrorResponse(Error error)
        : this(error.Code, error.Message, error.Details)
    {
    }
}
