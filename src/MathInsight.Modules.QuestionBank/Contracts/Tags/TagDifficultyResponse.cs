namespace MathInsight.Modules.QuestionBank.Contracts.Tags;

public sealed record TagDifficultyResponse
(
    string DifficultyId,
    string DifficultyName,
    string? Description,
    int LevelValue,
    int DisplayOrder,
    bool IsActive
);
