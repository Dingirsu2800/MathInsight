using MediatR;

namespace MathInsight.Modules.Learning_Lecture.Commands.Lectures;

public record PublishLectureCommand(string LectureId, string TeacherId, bool IsAdmin) : IRequest<bool>;
