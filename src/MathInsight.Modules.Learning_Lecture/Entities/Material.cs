using System;
using System.Collections.Generic;

namespace MathInsight.Modules.Learning_Lecture.Entities;

public class Material
{
    public string MaterialId { get; set; } = default!;
    public string MaterialName { get; set; } = default!;
    public string FileUrl { get; set; } = default!;
    public string FileType { get; set; } = default!;
    public string TeacherId { get; set; } = default!;
    public string Status { get; set; } = "Active";
    public DateTime UploadedTime { get; set; }

    public ICollection<LectureMaterial> LectureMaterials { get; set; } = new List<LectureMaterial>();
}
