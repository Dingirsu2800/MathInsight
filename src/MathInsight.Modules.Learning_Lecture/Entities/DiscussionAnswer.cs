using System;
using System.Collections.Generic;

namespace MathInsight.Modules.Learning_Lecture.Entities;

public class DiscussionAnswer
{
    public string DiscussionAnswerId { get; set; } = default!;
    public string DiscussionQuestionId { get; set; } = default!;
    public string AccountId { get; set; } = default!;
    public string Content { get; set; } = default!;
    public string Status { get; set; } = "Active";
    public DateTime CreatedTime { get; set; }
    public DateTime UpdatedTime { get; set; }

    public DiscussionQuestion Question { get; set; } = default!;
    public ICollection<DiscussionReport> Reports { get; set; } = new List<DiscussionReport>();
}
