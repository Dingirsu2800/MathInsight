using System.Security.Claims;
using System.Text.Json;
using MathInsight.Modules.QuestionBank.Commands.ReportQuestion;
using MathInsight.Modules.QuestionBank.Contracts.Reports;
using MathInsight.Modules.QuestionBank.Errors;
using MathInsight.Modules.Testing.Persistence;
using MathInsight.Shared.Events;
using MathInsight.Shared.Questions;
using MathInsight.Shared.Results;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace MathInsight.Modules.Testing.Controllers;

[ApiController]
[Authorize(Roles = "Student")]
[Route("api/v1/tests")]
public sealed class TestingController : ControllerBase
{
    private readonly TestingDbContext _db;
    private readonly IMediator _mediator;

    public TestingController(TestingDbContext db, IMediator mediator)
    {
        _db = db;
        _mediator = mediator;
    }

    [HttpPost("{testId}/sessions")]
    public async Task<IActionResult> StartSession(string testId, CancellationToken cancellationToken)
    {
        var studentId = GetStudentId();
        if (studentId is null)
            return Unauthorized(new ApiErrorResponse(ApplicationErrors.AuthInvalidToken));

        var test = await _db.Tests.AsNoTracking()
            .FirstOrDefaultAsync(item => item.TestId == testId && item.TestStatus == "Active", cancellationToken);
        if (test is null)
            return NotFound(new { code = "TEST_NOT_FOUND", message = "Test was not found or is inactive." });

        var session = new TestSession
        {
            SessionId = Guid.NewGuid().ToString("D"),
            TestId = test.TestId,
            StudentId = studentId,
            TestFormat = test.TestMode is "BlueprintExam" or "Diagnostic" ? "Exam" : "Practice",
            Status = "InProgress",
            Duration = 0,
            StartTime = DateTime.UtcNow,
            TotalQuestion = test.TotalQuestions
        };

        _db.TestSessions.Add(session);
        await _db.SaveChangesAsync(cancellationToken);
        return StatusCode(StatusCodes.Status201Created, new
        {
            session.SessionId,
            session.TestId,
            session.Status,
            session.StartTime
        });
    }

    [HttpGet("sessions/{sessionId}")]
    public async Task<IActionResult> GetSession(string sessionId, CancellationToken cancellationToken)
    {
        var studentId = GetStudentId();
        if (studentId is null)
            return Unauthorized(new ApiErrorResponse(ApplicationErrors.AuthInvalidToken));

        var session = await _db.TestSessions.AsNoTracking()
            .FirstOrDefaultAsync(item => item.SessionId == sessionId, cancellationToken);
        if (session is null)
            return NotFound(new { code = "TEST_SESSION_NOT_FOUND", message = "Test session was not found." });
        if (!string.Equals(session.StudentId, studentId, StringComparison.Ordinal))
            return Forbid();

        var test = await _db.Tests.AsNoTracking()
            .FirstAsync(item => item.TestId == session.TestId, cancellationToken);
        var rows = await LoadSnapshotRowsAsync(session.TestId, cancellationToken);

        return Ok(new TestSessionViewResponse(
            session.SessionId,
            session.TestId,
            test.TestName,
            session.Status,
            test.DurationMinutes,
            test.MaxScore,
            rows.Select(row => ToStudentQuestion(row.TestQuestion, row.Version)).ToList()));
    }

    [HttpPost("sessions/{sessionId}/submit")]
    public async Task<IActionResult> SubmitSession(
        string sessionId,
        [FromBody] SubmitSessionRequest? request,
        CancellationToken cancellationToken)
    {
        var studentId = GetStudentId();
        if (studentId is null)
            return Unauthorized(new ApiErrorResponse(ApplicationErrors.AuthInvalidToken));
        if (request is null)
            return BadRequest(new { code = "TEST_SUBMISSION_INVALID", message = "Submission payload is required." });

        var session = await _db.TestSessions
            .FirstOrDefaultAsync(item => item.SessionId == sessionId, cancellationToken);
        if (session is null)
            return NotFound(new { code = "TEST_SESSION_NOT_FOUND", message = "Test session was not found." });
        if (!string.Equals(session.StudentId, studentId, StringComparison.Ordinal))
            return Forbid();
        if (session.Status != "InProgress")
            return Conflict(new { code = "TEST_SESSION_ALREADY_SUBMITTED", message = "Test session is no longer in progress." });

        var rows = await LoadSnapshotRowsAsync(session.TestId, cancellationToken);
        var submittedByQuestion = (request.Answers ?? [])
            .GroupBy(item => item.QuestionId, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.Last(), StringComparer.OrdinalIgnoreCase);

        if (submittedByQuestion.Keys.Any(questionId => rows.All(row =>
                !string.Equals(row.TestQuestion.QuestionId, questionId, StringComparison.OrdinalIgnoreCase))))
        {
            return BadRequest(new { code = "ANSWER_NOT_IN_TEST_VERSION", message = "An answer references a question outside this test." });
        }

        var now = DateTime.UtcNow;
        foreach (var row in rows)
        {
            var snapshot = DeserializeSnapshot(row.Version);
            submittedByQuestion.TryGetValue(row.TestQuestion.QuestionId, out var submitted);
            var validationError = ValidateAnswer(snapshot, submitted);
            if (validationError is not null)
                return BadRequest(new { code = "ANSWER_NOT_IN_TEST_VERSION", message = validationError });

            var testAnswer = new TestAnswer
            {
                TestAnswerId = Guid.NewGuid().ToString("D"),
                SessionId = session.SessionId,
                QuestionId = row.TestQuestion.QuestionId,
                QuestionNo = row.TestQuestion.QuestionOrder,
                AnswerId = submitted?.AnswerId,
                ShortAnswerText = submitted?.ShortAnswerText,
                TimeSpent = submitted?.TimeSpent,
                FirstChoiceTime = submitted is null ? null : now,
                UpdateChoiceTime = submitted is null ? null : now
            };
            _db.TestAnswers.Add(testAnswer);

            foreach (var answerId in submitted?.SelectedOptionIds ?? [])
                _db.TestAnswerOptions.Add(new TestAnswerOption { TestAnswerId = testAnswer.TestAnswerId, AnswerId = answerId });

            foreach (var part in submitted?.Parts ?? [])
            {
                _db.TestAnswerParts.Add(new TestAnswerPart
                {
                    TestAnswerId = testAnswer.TestAnswerId,
                    PartId = part.PartId,
                    BooleanAnswer = part.BooleanAnswer,
                    TextAnswer = part.TextAnswer,
                    NumericAnswer = part.NumericAnswer
                });
            }
        }

        session.EndTime = now;
        session.Duration = Math.Max(0, (int)Math.Round((now - session.StartTime).TotalMinutes));
        await _db.SaveChangesAsync(cancellationToken);

        try
        {
            await _mediator.Publish(new TestSubmittedEvent
            {
                SessionId = session.SessionId,
                StudentId = session.StudentId,
                TestId = session.TestId,
                TestFormat = session.TestFormat,
                SubmissionType = "StudentSubmit",
                SubmittedTime = now
            }, cancellationToken);
        }
        catch (Exception) when (!cancellationToken.IsCancellationRequested)
        {
            await _db.Entry(session).ReloadAsync(cancellationToken);
            if (session.Status != "Graded")
            {
                await RemoveUnprocessedSubmissionAsync(session, cancellationToken);
                return StatusCode(StatusCodes.Status503ServiceUnavailable, new
                {
                    code = "GRADING_FAILED",
                    message = "The submission could not be graded. The student may submit it again."
                });
            }
        }

        await _db.Entry(session).ReloadAsync(cancellationToken);
        if (session.Status != "Graded")
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new
            {
                code = "GRADING_FAILED",
                message = "The submission was saved but grading did not complete. It can be retried."
            });

        return Ok(new { session.SessionId, session.Status, session.Score, session.GradeRevision });
    }

    [HttpPost("sessions/{sessionId}/questions/{questionId}/report")]
    public async Task<IActionResult> ReportSessionQuestion(
        string sessionId,
        string questionId,
        [FromBody] ReportQuestionRequest? request,
        CancellationToken cancellationToken)
    {
        var studentId = GetStudentId();
        if (studentId is null)
            return Unauthorized(new ApiErrorResponse(ApplicationErrors.AuthInvalidToken));
        if (request is null)
            return BadRequest(new ApiErrorResponse(QuestionBankErrors.ReportReasonRequired));

        var session = await _db.TestSessions.AsNoTracking()
            .FirstOrDefaultAsync(item => item.SessionId == sessionId, cancellationToken);
        if (session is null)
            return NotFound(new { code = "TEST_SESSION_NOT_FOUND", message = "Test session was not found." });
        if (!string.Equals(session.StudentId, studentId, StringComparison.Ordinal))
            return Forbid();

        var testQuestion = await _db.TestQuestions.AsNoTracking()
            .FirstOrDefaultAsync(item => item.TestId == session.TestId && item.QuestionId == questionId, cancellationToken);
        if (testQuestion is null)
            return BadRequest(new ApiErrorResponse(QuestionBankErrors.ReportSessionContextInvalid));

        var result = await _mediator.Send(new ReportQuestionCommand(
            questionId,
            request,
            studentId,
            "Student",
            session.SessionId,
            testQuestion.QuestionVersionId), cancellationToken);

        if (result.IsFailure)
            return result.Error == QuestionBankErrors.ReportAlreadyPending
                ? Conflict(new ApiErrorResponse(result.Error))
                : BadRequest(new ApiErrorResponse(result.Error!));

        return StatusCode(StatusCodes.Status201Created, result.Value);
    }

    private async Task<List<SnapshotRow>> LoadSnapshotRowsAsync(string testId, CancellationToken cancellationToken)
    {
        return await (
            from testQuestion in _db.TestQuestions.AsNoTracking()
            join version in _db.QuestionVersions.AsNoTracking()
                on testQuestion.QuestionVersionId equals version.VersionId
            where testQuestion.TestId == testId
            orderby testQuestion.QuestionOrder
            select new SnapshotRow(testQuestion, version))
            .ToListAsync(cancellationToken);
    }

    private async Task RemoveUnprocessedSubmissionAsync(
        TestSession session,
        CancellationToken cancellationToken)
    {
        var answers = await _db.TestAnswers
            .Where(item => item.SessionId == session.SessionId)
            .ToListAsync(cancellationToken);
        var answerIds = answers.Select(item => item.TestAnswerId).ToList();
        if (answerIds.Count > 0)
        {
            var options = await _db.TestAnswerOptions
                .Where(item => answerIds.Contains(item.TestAnswerId))
                .ToListAsync(cancellationToken);
            var parts = await _db.TestAnswerParts
                .Where(item => answerIds.Contains(item.TestAnswerId))
                .ToListAsync(cancellationToken);
            _db.TestAnswerOptions.RemoveRange(options);
            _db.TestAnswerParts.RemoveRange(parts);
            _db.TestAnswers.RemoveRange(answers);
        }

        session.EndTime = null;
        session.Duration = 0;
        await _db.SaveChangesAsync(cancellationToken);
    }

    private static StudentQuestionResponse ToStudentQuestion(
        TestQuestionReadModel testQuestion,
        QuestionVersionReadModel version)
    {
        var snapshot = DeserializeSnapshot(version);
        return new StudentQuestionResponse(
            snapshot.QuestionId,
            version.VersionId,
            testQuestion.QuestionOrder,
            snapshot.QuestionType,
            snapshot.QuestionContent ?? version.QuestionContent,
            snapshot.PictureUrl ?? version.PictureUrl,
            testQuestion.MaxPointsSnapshot,
            snapshot.Answers.Select(answer => new StudentAnswerOptionResponse(answer.AnswerId, answer.AnswerContent)).ToList(),
            snapshot.Parts.OrderBy(part => part.PartOrder)
                .Select(part => new StudentQuestionPartResponse(
                    part.PartId, part.PartOrder, part.PartLabel, part.PartContent, part.PartType))
                .ToList());
    }

    private static string? ValidateAnswer(QuestionSnapshotV2 snapshot, SubmittedAnswerRequest? answer)
    {
        if (answer is null)
            return null;

        var answerIds = snapshot.Answers.Select(item => item.AnswerId).ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (!string.IsNullOrWhiteSpace(answer.AnswerId) && !answerIds.Contains(answer.AnswerId))
            return $"Answer '{answer.AnswerId}' does not belong to question version '{snapshot.QuestionId}'.";
        if ((answer.SelectedOptionIds ?? []).Any(id => !answerIds.Contains(id)))
            return "At least one selected option does not belong to the immutable question version.";

        var partIds = snapshot.Parts.Select(item => item.PartId).ToHashSet(StringComparer.OrdinalIgnoreCase);
        if ((answer.Parts ?? []).Any(part => !partIds.Contains(part.PartId)))
            return "At least one submitted part does not belong to the immutable question version.";
        if ((answer.Parts ?? []).GroupBy(part => part.PartId, StringComparer.OrdinalIgnoreCase).Any(group => group.Count() > 1))
            return "A question part may only be submitted once.";

        return null;
    }

    private static QuestionSnapshotV2 DeserializeSnapshot(QuestionVersionReadModel version)
    {
        if (version.SnapshotSchemaVersion != 2)
            throw new InvalidOperationException($"Unsupported snapshot schema for version '{version.VersionId}'.");
        return JsonSerializer.Deserialize<QuestionSnapshotV2>(version.AnswersSnapshot)
            ?? throw new InvalidOperationException($"Invalid snapshot JSON for version '{version.VersionId}'.");
    }

    private string? GetStudentId()
        => User.FindFirst("account_id")?.Value
            ?? User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

    private sealed record SnapshotRow(TestQuestionReadModel TestQuestion, QuestionVersionReadModel Version);
}

public sealed record SubmitSessionRequest(IReadOnlyList<SubmittedAnswerRequest>? Answers);
public sealed record SubmittedAnswerRequest(
    string QuestionId,
    string? AnswerId,
    IReadOnlyList<string>? SelectedOptionIds,
    string? ShortAnswerText,
    IReadOnlyList<SubmittedPartRequest>? Parts,
    int? TimeSpent);
public sealed record SubmittedPartRequest(
    string PartId,
    bool? BooleanAnswer,
    string? TextAnswer,
    decimal? NumericAnswer);
public sealed record TestSessionViewResponse(
    string SessionId,
    string TestId,
    string TestName,
    string Status,
    int DurationMinutes,
    decimal MaxScore,
    IReadOnlyList<StudentQuestionResponse> Questions);
public sealed record StudentQuestionResponse(
    string QuestionId,
    string QuestionVersionId,
    int QuestionNo,
    string QuestionType,
    string QuestionContent,
    string? PictureUrl,
    decimal MaxPoints,
    IReadOnlyList<StudentAnswerOptionResponse> AnswerOptions,
    IReadOnlyList<StudentQuestionPartResponse> Parts);
public sealed record StudentAnswerOptionResponse(string AnswerId, string AnswerContent);
public sealed record StudentQuestionPartResponse(
    string PartId,
    int PartOrder,
    string? PartLabel,
    string PartContent,
    string PartType);
