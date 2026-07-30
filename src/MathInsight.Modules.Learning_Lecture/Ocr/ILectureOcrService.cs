using System.IO;
using System.Threading;
using System.Threading.Tasks;
using MathInsight.Shared.Results;

namespace MathInsight.Modules.Learning_Lecture.Ocr;

public interface ILectureOcrService
{
    Task<Result<string>> ExtractMarkdownAsync(
        Stream image,
        string contentType,
        CancellationToken cancellationToken);
}
