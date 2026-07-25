namespace MathInsight.Modules.Grading_Analytics.Services;

/// <summary>
/// AI chatbot service for providing step-by-step explanations to students (UC-51).
/// </summary>
public interface IChatbotService
{
    /// <summary>
    /// Sends question content and student answer to an AI API, returning a natural-language
    /// step-by-step explanation. The response is NOT persisted to the database (BR-21).
    /// </summary>
    /// <param name="questionContent">The question text to explain.</param>
    /// <param name="studentAnswer">The student's selected/entered answer.</param>
    /// <param name="studentId">Student ID — used for rate limiting.</param>
    /// <param name="sessionId">Session ID — used for rate limiting.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A step-by-step explanation string.</returns>
    Task<string> AskAsync(
        string questionContent,
        string studentAnswer,
        string studentId,
        string sessionId,
        CancellationToken cancellationToken = default);
}
