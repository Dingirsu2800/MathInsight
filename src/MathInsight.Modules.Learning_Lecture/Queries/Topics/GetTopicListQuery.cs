using System.Collections.Generic;
using MediatR;
using MathInsight.Modules.Learning_Lecture.Contracts;

namespace MathInsight.Modules.Learning_Lecture.Queries.Topics;

public record GetTopicListQuery(int? Grade) : IRequest<List<TopicDto>>;
