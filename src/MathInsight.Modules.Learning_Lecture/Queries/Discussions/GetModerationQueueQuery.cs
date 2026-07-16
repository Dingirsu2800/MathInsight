using System.Collections.Generic;
using MediatR;
using MathInsight.Modules.Learning_Lecture.Contracts;

namespace MathInsight.Modules.Learning_Lecture.Queries.Discussions;

public record GetModerationQueueQuery(string? TeacherId, int Page, int PageSize) : IRequest<List<DiscussionReportDto>>;
