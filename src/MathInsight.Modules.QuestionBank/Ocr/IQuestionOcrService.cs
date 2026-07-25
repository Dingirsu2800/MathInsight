using MathInsight.Modules.QuestionBank.Contracts.Questions;

namespace MathInsight.Modules.QuestionBank.Ocr;

public interface IQuestionOcrService
{
    Task<QuestionOcrDraftResponse> ExtractDraftAsync(
        Stream image,
        string contentType,
        CancellationToken cancellationToken);
}
