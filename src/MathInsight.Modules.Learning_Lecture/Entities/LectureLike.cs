using System;

namespace MathInsight.Modules.Learning_Lecture.Entities;

public class LectureLike
{
    public string LectureId { get; set; } = default!;
    public string StudentId { get; set; } = default!;
    public DateTime CreatedTime { get; set; }

    public Lecture Lecture { get; set; } = default!;
}
