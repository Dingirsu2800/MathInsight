namespace MathInsight.Shared.Scoring;

/// <summary>
/// Cross-module boundary used by QuestionBank after an expert confirms that a
/// student-reported question version is invalid.
/// </summary>
public interface IScoreAdjustmentService
{
    Task AdjustInvalidQuestionVersionAsync(
        string reportId,
        CancellationToken cancellationToken = default);
}
