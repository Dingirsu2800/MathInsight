using MediatR;
using MathInsight.Modules.Learning_Lecture.Contracts;

namespace MathInsight.Modules.Learning_Lecture.Commands.Lectures;

public record CreateLectureCommand(
    string Title,
    string? Content,
    string? VideoUrl,
    string? ThumbnailUrl,
    string TagId,
    string TeacherId,
    System.Collections.Generic.List<string>? MaterialIds
) : IRequest<LectureDto>;
