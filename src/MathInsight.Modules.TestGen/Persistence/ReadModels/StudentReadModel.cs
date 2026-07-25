namespace MathInsight.Modules.TestGen.Persistence.ReadModels;

public sealed class StudentReadModel
{
    public string StudentId { get; set; } = string.Empty;
    public int? CurrentGrade { get; set; }

    public ICollection<Entities.Test> GeneratedTests { get; set; } = new List<Entities.Test>();
}
