using MediatR;

namespace MathInsight.Modules.Learning_Lecture.Commands.Materials;

public record AttachMaterialToLectureCommand(string MaterialId, string LectureId, string TeacherId) : IRequest<bool>;
