using System.IO;
using System.Threading;
using System.Threading.Tasks;
using MathInsight.Shared.Results;

namespace MathInsight.Modules.Learning_Lecture.Services;

public interface ILectureDocumentParserService
{
    Task<Result<string>> ParseDocxAsync(Stream docxStream, CancellationToken cancellationToken);
}
