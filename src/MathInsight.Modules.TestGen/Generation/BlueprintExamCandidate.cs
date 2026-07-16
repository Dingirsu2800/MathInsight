namespace MathInsight.Modules.TestGen.Generation;

public sealed record BlueprintExamCandidate(
    string QuestionId,
    string DifficultyId,
    string QuestionType,
    IReadOnlySet<string> TagIds);

public sealed record BlueprintExamRequirement(
    string BlueprintDetailId,
    int SectionOrder,
    int DetailOrder,
    string TagId,
    string DifficultyId,
    string QuestionType,
    int Quantity);

public sealed record BlueprintExamAssignment(
    string QuestionId,
    string BlueprintDetailId,
    int SectionOrder,
    int DetailOrder,
    int CandidateOrder);

public sealed record BlueprintExamSelection(
    bool IsComplete,
    IReadOnlyList<BlueprintExamAssignment> Assignments);
