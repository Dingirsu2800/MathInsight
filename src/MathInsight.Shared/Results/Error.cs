namespace MathInsight.Shared.Results;

public sealed record Error(string Code, string Message, object? Details = null);

