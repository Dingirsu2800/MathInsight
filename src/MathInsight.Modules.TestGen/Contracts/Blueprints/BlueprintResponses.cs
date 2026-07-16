namespace MathInsight.Modules.TestGen.Contracts.Blueprints;

public sealed record CreateBlueprintResponse(
    string BlueprintId,
    string Status);

public sealed record UpdateBlueprintResponse(
    string BlueprintId,
    string Status);

public sealed record SubmitBlueprintResponse(
    string BlueprintId,
    string Status);

public sealed record ReviewBlueprintResponse(
    string BlueprintId,
    string Status,
    string ReviewedBy,
    DateTime ReviewTime);

public sealed record CloneBlueprintResponse(
    string BlueprintId,
    string BlueprintName,
    string Status);

public sealed record DeleteBlueprintResponse(
    string BlueprintId,
    bool WasDeactivated,
    string? Status);

public sealed record BlueprintListItemResponse(
    string BlueprintId,
    string BlueprintName,
    int Grade,
    int TotalQuestions,
    int DurationMinutes,
    string ExpertId,
    string? ExpertName,
    string Status,
    int SectionCount,
    int DetailSlotCount);

public sealed record BlueprintDetailResponse(
    string BlueprintId,
    string BlueprintName,
    int Grade,
    int TotalQuestions,
    int DurationMinutes,
    string ExpertId,
    string? ExpertName,
    string Status,
    string? ApprovedBy,
    string? ApprovedByName,
    string? ReviewNote,
    DateTime? ReviewTime,
    IReadOnlyList<BlueprintSectionResponse> Sections);

public sealed record BlueprintSectionResponse(
    string BlueprintSectionId,
    int SectionOrder,
    string? SectionCode,
    string SectionName,
    string QuestionType,
    string? InstructionText,
    int TotalQuestions,
    decimal DefaultPointPerQuestion,
    decimal? DefaultPointPerPart,
    int? PartCountPerQuestion,
    IReadOnlyList<BlueprintDetailSlotResponse> Details);

public sealed record BlueprintDetailSlotResponse(
    string BlueprintDetailId,
    string TagId,
    string? TagName,
    string DifficultyId,
    string? DifficultyName,
    int? DifficultyLevel,
    int Quantity);
