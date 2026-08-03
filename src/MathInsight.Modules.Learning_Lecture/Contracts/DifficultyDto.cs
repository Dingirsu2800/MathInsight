namespace MathInsight.Modules.Learning_Lecture.Contracts;

public sealed record DifficultyDto(
    string DifficultyId,
    string DifficultyName,
    int LevelValue,
    int DisplayOrder);
