using System;
using System.Collections.Generic;

namespace MathInsight.Modules.Learning_Lecture.Entities;

public class DiscussionQuestion
{
    public string DiscussionQuestionId { get; set; } = default!;
    public string LectureId { get; set; } = default!;
    public string StudentId { get; set; } = default!;
    public string Title { get; set; } = default!;
    public string Content { get; set; } = default!;
    public string Status { get; set; } = "Active";
    public DateTime CreatedTime { get; set; }
    public DateTime UpdatedTime { get; set; }

    public Lecture Lecture { get; set; } = default!;
    public ICollection<DiscussionAnswer> Answers { get; set; } = new List<DiscussionAnswer>();
    public ICollection<DiscussionReport> Reports { get; set; } = new List<DiscussionReport>();
}
