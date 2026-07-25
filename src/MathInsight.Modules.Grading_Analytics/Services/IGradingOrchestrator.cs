using MathInsight.Shared.Events;

namespace MathInsight.Modules.Grading_Analytics.Services;

/// <summary>
/// Orchestrates the full grading flow for a test session:
///   1. Load session with all navigation properties
///   2. Validate status = InProgress
///   3. Run GradingEngine.Grade()
///   4. Save results in a single transaction (DC-05)
///   5. Return GradeCalculatedEvent for downstream publishing
///
/// Shared by both:
///   - GradeSubmittedSessionHandler (MediatR in-process, Practice mode)
///   - TestSubmittedConsumer (MassTransit async, Exam mode)
/// </summary>
public interface IGradingOrchestrator
{
    /// <summary>
    /// Grades the session identified by <paramref name="sessionId"/> within an EF execution
    /// strategy (retry-safe). Returns a <see cref="GradeCalculatedEvent"/> to publish after
    /// commit, or null if the session was not found or not in InProgress status.
    /// </summary>
    Task<GradeCalculatedEvent?> GradeSessionAsync(
        string sessionId,
        TestSubmittedEvent notification,
        CancellationToken cancellationToken = default);
}
