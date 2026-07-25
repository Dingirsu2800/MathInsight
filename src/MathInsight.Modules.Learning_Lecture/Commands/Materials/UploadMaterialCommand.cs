using System.IO;
using MediatR;
using MathInsight.Modules.Learning_Lecture.Contracts;

namespace MathInsight.Modules.Learning_Lecture.Commands.Materials;

public record UploadMaterialCommand(
    string MaterialName,
    Stream FileStream,
    string FileName,
    string TeacherId
) : IRequest<MaterialDto>;
