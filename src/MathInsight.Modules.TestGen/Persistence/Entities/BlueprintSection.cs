using System;
using System.Collections.Generic;

namespace MathInsight.Modules.TestGen.Persistence.Entities;

/// <summary>
/// Blueprint section entity owned by TestGen module.
/// Maps to SQL table: BlueprintSection.
/// </summary>
public class BlueprintSection
{
    public Guid BlueprintSectionId { get; set; }
    public Guid BlueprintId { get; set; }
    public int SectionOrder { get; set; }
    public string? SectionCode { get; set; }
    public string? SectionName { get; set; }
    
    /// <summary>SingleChoice | MultipleChoice | TrueFalse | ShortAnswer | Composite</summary>
    public string QuestionType { get; set; } = string.Empty;
    
    public string? InstructionText { get; set; }
    public int TotalQuestions { get; set; }
    public decimal? DefaultPointPerQuestion { get; set; }
    public decimal? DefaultPointPerPart { get; set; }
    public int? PartCountPerQuestion { get; set; }

    // Navigation
    public Blueprint? Blueprint { get; set; }
    public ICollection<BlueprintDetail> Details { get; set; } = new List<BlueprintDetail>();
}
