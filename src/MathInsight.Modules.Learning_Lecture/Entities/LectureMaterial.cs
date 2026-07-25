namespace MathInsight.Modules.Learning_Lecture.Entities;

public class LectureMaterial
{
    public string LectureId { get; set; } = default!;
    public string MaterialId { get; set; } = default!;

    public Lecture Lecture { get; set; } = default!;
    public Material Material { get; set; } = default!;
}
