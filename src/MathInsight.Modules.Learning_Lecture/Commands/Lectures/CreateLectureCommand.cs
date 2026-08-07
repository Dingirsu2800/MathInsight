using MediatR;
using MathInsight.Modules.Learning_Lecture.Contracts;
using MathInsight.Shared.Results;

namespace MathInsight.Modules.Learning_Lecture.Commands.Lectures;

public record CreateLectureCommand(
    string Title,
    string? Content,
    string? VideoUrl,
    string? ThumbnailUrl,
    string TagId,
    string? DifficultyId,
    string TeacherId,
    System.Collections.Generic.List<string>? MaterialIds,
    string? NextLectureId = null
) : IRequest<Result<LectureDto>>;
