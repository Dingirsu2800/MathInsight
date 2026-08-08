using MediatR;

using MathInsight.Shared.Results;

namespace MathInsight.Modules.Learning_Lecture.Commands.Lectures;

public record PublishLectureCommand(string LectureId, string TeacherId, bool IsAdmin) : IRequest<Result<bool>>;
