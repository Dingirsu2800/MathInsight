using System;

namespace MathInsight.Modules.Learning_Lecture.Contracts;

public class MaterialDto
{
    public string MaterialId { get; set; } = default!;
    public string MaterialName { get; set; } = default!;
    public string FileUrl { get; set; } = default!;
    public string FileType { get; set; } = default!;
    public string TeacherId { get; set; } = default!;
    public string Status { get; set; } = default!;
    public DateTime UploadedTime { get; set; }
    public string? LectureName { get; set; }
}
