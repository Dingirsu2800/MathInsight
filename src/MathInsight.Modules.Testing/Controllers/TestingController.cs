using Microsoft.AspNetCore.Mvc;
using MediatR;
using MassTransit;
using MathInsight.Shared.Events;

namespace MathInsight.Modules.Testing.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
public class TestingController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly IPublishEndpoint _publishEndpoint;

    public TestingController(IMediator mediator, IPublishEndpoint publishEndpoint)
    {
        _mediator = mediator;
        _publishEndpoint = publishEndpoint;
    }

    [HttpPost("submit")]
    public async Task<IActionResult> SubmitTest([FromBody] SubmitTestRequest request)
    {
        // 1. Save Test Session to "tst.TestSessions" schema
        var sessionId = Guid.NewGuid();
        
        var submissionEvent = new TestSubmittedEvent
        {
            SessionId = sessionId,
            StudentId = request.StudentId,
            TestId = request.TestId,
            TestFormat = request.TestFormat,
            Answers = request.Answers,
            SubmittedTime = DateTime.UtcNow
        };

        if (request.TestFormat == "Practice")
        {
            // For Practice mode, grade synchronously in-memory using MediatR
            await _mediator.Publish(submissionEvent);
            return Ok(new { SessionId = sessionId, Status = "GradedSynchronously" });
        }
        else
        {
            // For Exam mode, publish to RabbitMQ using MassTransit for background queue grading
            await _publishEndpoint.Publish(submissionEvent);
            return Accepted(new { SessionId = sessionId, Status = "QueuedForBackgroundGrading" });
        }
    }
}

public record SubmitTestRequest(Guid StudentId, Guid TestId, string TestFormat, Dictionary<Guid, string> Answers);