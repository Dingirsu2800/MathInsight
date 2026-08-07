namespace MathInsight.Modules.Recommender.Persistence.Entities;

/// <summary>
/// Read-only entity for cross-module query: maps to the Lecture table owned by Learning_Lecture module.
/// Used by Recommender to rank available lectures by topic and difficulty.
/// This entity is NOT owned by Recommender — no writes should be made through it.
/// </summary>
public class LectureReadOnly
{
    public string LectureId { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string? ThumbnailUrl { get; set; }
    public int Likes { get; set; }
    public string TagId { get; set; } = string.Empty;
    public string? DifficultyId { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime UpdatedTime { get; set; }
}
