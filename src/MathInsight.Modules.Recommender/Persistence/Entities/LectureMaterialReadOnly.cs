namespace MathInsight.Modules.Recommender.Persistence.Entities;

/// <summary>
/// Read-only entity for cross-module query: maps to the LectureMaterial join table.
/// Used by Recommender to match weak TagIDs to materials via Lectures (UC-54, RCM-10).
/// Lecture-Material relationship is many-to-many per current ERD.
/// This entity is NOT owned by Recommender — no writes should be made through it.
/// </summary>
public class LectureMaterialReadOnly
{
    public string LectureId { get; set; } = string.Empty;
    public string MaterialId { get; set; } = string.Empty;
}
