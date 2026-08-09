using MathInsight.Shared.Results;
using MediatR;

namespace MathInsight.Modules.Learning_Lecture.Commands.Lectures;

public record LogLectureViewCommand(string LectureId, string StudentId, int DurationSeconds) : IRequest<Result<bool>>;
