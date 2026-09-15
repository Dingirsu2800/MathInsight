namespace MathInsight.Modules.TestGen.Contracts.Blueprints;

public sealed record BlueprintAvailabilityResponse(
    DateTimeOffset CheckedAt,
    IReadOnlyList<BlueprintAvailabilitySectionResponse> Sections,
    bool WholeBlueprintFeasible,
    string? AvailabilityCode);

public sealed record BlueprintAvailabilitySectionResponse(
    string ClientSectionId,
    IReadOnlyList<BlueprintAvailabilityRowResponse> Rows);

public sealed record BlueprintAvailabilityRowResponse(
    string ClientRowId,
    int AvailableCount,
    int RequiredCount,
    int Shortage);
