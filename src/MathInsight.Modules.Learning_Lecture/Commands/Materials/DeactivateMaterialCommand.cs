using MediatR;

namespace MathInsight.Modules.Learning_Lecture.Commands.Materials;

public record DeactivateMaterialCommand(string MaterialId, string TeacherId) : IRequest<bool>;
