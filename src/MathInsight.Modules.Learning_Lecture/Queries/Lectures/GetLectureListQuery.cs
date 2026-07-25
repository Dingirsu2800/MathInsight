using System.Collections.Generic;
using MediatR;
using MathInsight.Modules.Learning_Lecture.Contracts;

namespace MathInsight.Modules.Learning_Lecture.Queries.Lectures;

public record GetLectureListQuery(string? TeacherId, bool IsStudent, int Page, int PageSize, string? Search = null, string? Status = null, string? Topic = null, int? Grade = null) : IRequest<PagedResult<LectureDto>>;
