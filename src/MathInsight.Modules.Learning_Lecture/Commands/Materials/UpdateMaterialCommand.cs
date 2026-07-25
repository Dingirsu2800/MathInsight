using MediatR;
using MathInsight.Modules.Learning_Lecture.Contracts;

namespace MathInsight.Modules.Learning_Lecture.Commands.Materials;

public record UpdateMaterialCommand(
    string MaterialId,
    string MaterialName,
    string TeacherId
) : IRequest<MaterialDto>;
