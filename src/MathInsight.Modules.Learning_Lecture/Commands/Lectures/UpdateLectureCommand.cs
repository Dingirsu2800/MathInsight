using MediatR;
using MathInsight.Modules.Learning_Lecture.Contracts;

namespace MathInsight.Modules.Learning_Lecture.Commands.Lectures;

public record UpdateLectureCommand(
    string LectureId,
    string Title,
    string? Content,
    string? VideoUrl,
    string? ThumbnailUrl,
    string TagId,
    string TeacherId
) : IRequest<LectureDto>;
