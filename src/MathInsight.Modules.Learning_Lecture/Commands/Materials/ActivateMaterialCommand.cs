using MediatR;

namespace MathInsight.Modules.Learning_Lecture.Commands.Materials;

public record ActivateMaterialCommand(string MaterialId, string TeacherId) : IRequest<bool>;
