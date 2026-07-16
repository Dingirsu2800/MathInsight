using System;
using System.Collections.Generic;

namespace MathInsight.Modules.Learning_Lecture.Contracts;

public class LectureDto
{
    public string LectureId { get; set; } = default!;
    public string Title { get; set; } = default!;
    public string? Content { get; set; }
    public string? VideoUrl { get; set; }
    public string? ThumbnailUrl { get; set; }
    public int Likes { get; set; }
    public string TeacherId { get; set; } = default!;
    public string TagId { get; set; } = default!;
    public string? TagName { get; set; }
    public bool IsLiked { get; set; }
    public string Status { get; set; } = default!;
    public DateTime CreatedTime { get; set; }
    public DateTime UpdatedTime { get; set; }
    public List<MaterialDto> Materials { get; set; } = new();
}
