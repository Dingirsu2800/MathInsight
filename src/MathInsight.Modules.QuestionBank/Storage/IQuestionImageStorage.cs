namespace MathInsight.Modules.QuestionBank.Storage;

public interface IQuestionImageStorage
{
    Task<string> UploadAsync(
        Stream content,
        string fileName,
        string contentType,
        CancellationToken cancellationToken);
}
