using System.Collections.Generic;
using MediatR;
using MathInsight.Modules.Learning_Lecture.Contracts;

namespace MathInsight.Modules.Learning_Lecture.Queries.Discussions;

public record GetDiscussionsQuery(string LectureId, bool IsStudent, string CurrentAccountId, int Page, int PageSize) : IRequest<List<DiscussionQuestionDto>>;
