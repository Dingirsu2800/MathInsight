using MediatR;
using MathInsight.Modules.Learning_Lecture.Contracts;

namespace MathInsight.Modules.Learning_Lecture.Queries.Lectures;

public record GetLectureQuery(string LectureId, string? StudentId) : IRequest<LectureDto>;
