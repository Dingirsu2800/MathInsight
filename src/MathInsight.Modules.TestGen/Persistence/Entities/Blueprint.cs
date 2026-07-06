using System;
using System.Collections.Generic;

namespace MathInsight.Modules.TestGen.Persistence.Entities;

/// <summary>
/// Blueprint entity owned by TestGen module.
/// Maps to SQL table: Blueprint.
/// </summary>
public class Blueprint
{
    public Guid BlueprintId { get; set; }
    public string BlueprintName { get; set; } = string.Empty;
    public int Grade { get; set; } // 10 | 11 | 12
    public int TotalQuestions { get; set; }
    public int DurationMinutes { get; set; }
    public Guid ExpertId { get; set; }
    
    /// <summary>DRAFT | PENDING_REVIEW | APPROVED | REJECTED | ACTIVE</summary>
    public string Status { get; set; } = "DRAFT";
    
    public string? ReviewNote { get; set; }
    public Guid? ReviewedBy { get; set; }
    public DateTime? ReviewedTime { get; set; }
    public DateTime CreatedTime { get; set; } = DateTime.UtcNow;

    // Navigation
    public ICollection<BlueprintSection> Sections { get; set; } = new List<BlueprintSection>();
}
