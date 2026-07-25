using MediatR;

namespace MathInsight.Modules.Learning_Lecture.Commands.Lectures;

public record DeactivateLectureCommand(string LectureId, string TeacherId, bool IsAdmin) : IRequest<bool>;
