using MathInsight.Modules.QuestionBank.Contracts.Tags;
using MathInsight.Modules.QuestionBank.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace MathInsight.Modules.QuestionBank.Queries.GetTagTopics;

public class GetTagTopicTreeQueryHandler : IRequestHandler<GetTagTopicTreeQuery, IReadOnlyList<TagTopicTreeResponse>>
{
    private readonly QuestionBankDbContext _context;

    public GetTagTopicTreeQueryHandler(QuestionBankDbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<TagTopicTreeResponse>> Handle(GetTagTopicTreeQuery request, CancellationToken cancellationToken)
    {
        var query = _context.TagTopics
        .AsNoTracking()
        .Where(topic => topic.IsActive);

        if (request.Grade is not null)
        {
            query = query.Where(topic => topic.Grade == request.Grade);
        }

        var topics = await query
            .OrderBy(topic => topic.DisplayOrder)
            .ToListAsync(cancellationToken);

        TagTopicTreeResponse BuildNode(string tagId)
        {
            var topic = topics.Single(topic => topic.TagId == tagId);

            var children = topics
                .Where(child => child.ParentTagId == topic.TagId)
                .OrderBy(child => child.DisplayOrder)
                .Select(child => BuildNode(child.TagId))
                .ToList();

            return new TagTopicTreeResponse(
                topic.TagId,
                topic.ParentTagId,
                topic.TagName,
                topic.Description,
                topic.Grade,
                topic.DisplayOrder,
                topic.IsActive,
                children
            );
        }

        return topics
            .Where(topic => topic.ParentTagId is null)
            .OrderBy(topic => topic.DisplayOrder)
            .Select(topic => BuildNode(topic.TagId))
            .ToList();
    }
}
