using System.IO;
using System.Threading.Tasks;

namespace MathInsight.Modules.Learning_Lecture.Services;

public interface ICloudinaryService
{
    Task<string> UploadAsync(Stream fileStream, string fileName);
}
