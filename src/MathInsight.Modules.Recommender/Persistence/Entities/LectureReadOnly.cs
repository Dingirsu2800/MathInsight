namespace MathInsight.Modules.Recommender.Persistence.Entities;

/// <summary>
/// Read-only entity for cross-module query: maps to the Lecture table owned by Learning_Lecture module.
/// Used by Recommender to match weak TagIDs to available lectures (UC-53, RCM-10).
/// This entity is NOT owned by Recommender — no writes should be made through it.
/// </summary>
public class LectureReadOnly
{
    public string LectureId { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string? Content { get; set; }
    public string TagId { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
}
