using MathInsight.Modules.TestGen.Generation;

namespace MathInsight.Modules.TestGen.Commands.GenerateTopicPractice;

internal sealed record PreparedTopicPracticeGeneration(
    string TestId,
    string StudentId,
    string SelectedTagId,
    string SelectedTagName,
    string TestName,
    DateTime CreatedTime,
    TopicPracticeRecommendationContext Recommendation,
    string? RecommendedDifficultyId,
    string DifficultySelectionMode,
    string? SelectedDifficultyId,
    string? SelectedDifficultyName,
    byte? SelectedDifficultyLevel,
    IReadOnlyList<PreparedTopicPracticeQuestion> Questions);

internal sealed record PreparedTopicPracticeQuestion(
    BlueprintExamCandidate Question,
    int QuestionOrder,
    decimal MaxPoints,
    string ScoringRule,
    bool IsWeakTagFocus);
