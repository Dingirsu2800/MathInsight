using MediatR;

namespace MathInsight.Modules.Learning_Lecture.Commands.Lectures;

public record UnlikeLectureCommand(string LectureId, string StudentId) : IRequest<bool>;
