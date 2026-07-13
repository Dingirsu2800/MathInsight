using System.Collections.Generic;
using MediatR;
using MathInsight.Modules.Learning_Lecture.Contracts;

namespace MathInsight.Modules.Learning_Lecture.Queries.Lectures;

public record GetLectureListQuery(string? TeacherId, bool IsStudent, int Page, int PageSize) : IRequest<List<LectureDto>>;
