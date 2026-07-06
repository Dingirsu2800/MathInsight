using System;

namespace MathInsight.Modules.TestGen.Persistence.Entities;

/// <summary>
/// Blueprint detail entity owned by TestGen module.
/// Maps to SQL table: BlueprintDetail.
/// </summary>
public class BlueprintDetail
{
    public Guid BlueprintDetailId { get; set; }
    public Guid BlueprintId { get; set; }
    public Guid BlueprintSectionId { get; set; }
    public Guid TagId { get; set; }
    public Guid DifficultyId { get; set; }
    public int Quantity { get; set; }

    // Navigation
    public BlueprintSection? BlueprintSection { get; set; }
}
