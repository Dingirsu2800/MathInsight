using System;
using System.Collections.Generic;

namespace MathInsight.Modules.Learning_Lecture.Entities;

public class Lecture
{
    public string LectureId { get; set; } = default!;
    public string Title { get; set; } = default!;
    public string? Content { get; set; }
    public string? VideoUrl { get; set; }
    public string? ThumbnailUrl { get; set; }
    public int Likes { get; set; }
    public string TeacherId { get; set; } = default!;
    public string TagId { get; set; } = default!;
    public string Status { get; set; } = "Draft";
    public DateTime CreatedTime { get; set; }
    public DateTime UpdatedTime { get; set; }
    public string? NextLectureId { get; set; }
    public Lecture? NextLecture { get; set; }

    public ICollection<LectureMaterial> LectureMaterials { get; set; } = new List<LectureMaterial>();
    public ICollection<LectureLike> LectureLikes { get; set; } = new List<LectureLike>();
    public ICollection<DiscussionQuestion> DiscussionQuestions { get; set; } = new List<DiscussionQuestion>();
}
