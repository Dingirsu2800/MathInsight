namespace MathInsight.Modules.Identity_Access.Entities;

public class Expert
{
    public string ExpertId { get; set; } = default!;
    public string? Specialty { get; set; }
    public Account Account { get; set; } = default!;
}

