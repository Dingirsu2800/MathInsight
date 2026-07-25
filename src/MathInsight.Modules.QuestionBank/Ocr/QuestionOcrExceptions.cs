namespace MathInsight.Modules.QuestionBank.Ocr;

public sealed class QuestionOcrNotConfiguredException : Exception;

public sealed class QuestionOcrProviderUnavailableException : Exception
{
    public QuestionOcrProviderUnavailableException(Exception? innerException = null)
        : base("Mistral OCR provider is unavailable.", innerException)
    {
    }
}

public sealed class QuestionOcrProviderRateLimitedException : Exception;

public sealed class QuestionOcrTimeoutException : Exception;

public sealed class QuestionOcrInvalidResponseException : Exception
{
    public QuestionOcrInvalidResponseException(Exception? innerException = null)
        : base("Mistral OCR returned an invalid response.", innerException)
    {
    }
}

public sealed class QuestionOcrDraftUnavailableException : Exception;
