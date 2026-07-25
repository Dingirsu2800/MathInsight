namespace MathInsight.Modules.Recommender.Persistence.Entities;

/// <summary>
/// Read-only entity for cross-module query: maps to the Material table.
/// Used by Recommender to return recommended materials via LectureMaterial (UC-54, RCM-10).
/// This entity is NOT owned by Recommender — no writes should be made through it.
/// </summary>
public class MaterialReadOnly
{
    public string MaterialId { get; set; } = string.Empty;
    public string MaterialName { get; set; } = string.Empty;
    public string? FileUrl { get; set; }
    public string? FileType { get; set; }
    public string Status { get; set; } = "Active";
}
