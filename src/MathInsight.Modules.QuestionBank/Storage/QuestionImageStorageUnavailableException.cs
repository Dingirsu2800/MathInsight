namespace MathInsight.Modules.QuestionBank.Storage;

public sealed class QuestionImageStorageUnavailableException : Exception
{
    public QuestionImageStorageUnavailableException()
        : base("Question image storage is not configured.")
    {
    }
}
