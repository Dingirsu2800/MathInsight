using MediatR;

namespace MathInsight.Modules.Learning_Lecture.Commands.Lectures;

public record LikeLectureCommand(string LectureId, string StudentId) : IRequest<bool>;
