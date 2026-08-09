namespace MathInsight.Modules.Gamification.Contracts;

public class StudyHeatmapDto
{
    public List<HeatmapDayDto> Days { get; set; } = new();
}

public class HeatmapDayDto
{
    public string Date { get; set; } = default!; // Format: YYYY-MM-DD
    public int ActivityCount { get; set; }
}
