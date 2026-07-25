using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using MathInsight.Modules.Learning_Lecture.Contracts;
using MathInsight.Modules.Learning_Lecture.Persistence;

namespace MathInsight.Modules.Learning_Lecture.Queries.Topics;

public class GetTopicListQueryHandler : IRequestHandler<GetTopicListQuery, List<TopicDto>>
{
    private readonly LearningDbContext _dbContext;

    public GetTopicListQueryHandler(LearningDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<List<TopicDto>> Handle(GetTopicListQuery request, CancellationToken cancellationToken)
    {
        var query = _dbContext.Set<MathInsight.Modules.Learning_Lecture.Entities.TagTopicReadOnly>().AsNoTracking();

        if (request.Grade.HasValue)
        {
            query = query.Where(x => x.Grade == request.Grade.Value);
        }

        var tags = await query.ToListAsync(cancellationToken);

        var lookup = tags.ToLookup(x => x.ParentTagId);
        var rootTags = tags.Where(x => string.IsNullOrEmpty(x.ParentTagId) || !tags.Any(t => t.TagId == x.ParentTagId)).ToList();

        var result = new List<TopicDto>();
        foreach (var root in rootTags)
        {
            result.Add(BuildNode(root, lookup));
        }

        return result;
    }

    private TopicDto BuildNode(MathInsight.Modules.Learning_Lecture.Entities.TagTopicReadOnly node, ILookup<string?, MathInsight.Modules.Learning_Lecture.Entities.TagTopicReadOnly> lookup)
    {
        var dto = new TopicDto
        {
            TagId = node.TagId,
            TagName = node.TagName,
            ParentTagId = node.ParentTagId,
            Grade = node.Grade
        };

        foreach (var child in lookup[node.TagId])
        {
            dto.Children.Add(BuildNode(child, lookup));
        }

        return dto;
    }
}
