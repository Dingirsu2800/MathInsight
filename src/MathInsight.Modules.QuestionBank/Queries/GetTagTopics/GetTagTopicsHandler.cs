using MathInsight.Modules.QuestionBank.Contracts.Tags;
using MathInsight.Modules.QuestionBank.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace MathInsight.Modules.QuestionBank.Queries.GetTagTopics;

public class GetTagTopicsHandler : IRequestHandler<GetTagTopics, IReadOnlyList<TagTopicResponse>>
{
    private readonly QuestionBankDbContext _context;

    public GetTagTopicsHandler(QuestionBankDbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<TagTopicResponse>> Handle(GetTagTopics request, CancellationToken cancellationToken)
    {
        var result = _context.TagTopics
            .AsNoTracking()
            .Where(topic => topic.IsActive);

        if (request.Grade is not null)
            result = result.Where(topic => topic.Grade == request.Grade);

        return await result.OrderBy(topic => topic.DisplayOrder)
            .Select(topic => new TagTopicResponse
            {
                TagId = topic.TagId,
                ParentTagId = topic.ParentTagId,
                TagName = topic.TagName,
                Description = topic.Description,
                Grade = topic.Grade,
                DisplayOrder = topic.DisplayOrder,
                IsActive = topic.IsActive
            })
            .ToListAsync(cancellationToken);
    }
}
