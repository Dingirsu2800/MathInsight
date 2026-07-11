namespace MathInsight.Modules.QuestionBank.Storage;

public sealed class QuestionImageUploadException : Exception
{
    public QuestionImageUploadException()
        : base("Question image upload failed.")
    {
    }

    public QuestionImageUploadException(Exception innerException)
        : base("Question image upload failed.", innerException)
    {
    }
}
