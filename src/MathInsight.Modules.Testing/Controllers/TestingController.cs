using Microsoft.AspNetCore.Mvc;
using MediatR;
using MathInsight.Shared.Events;

namespace MathInsight.Modules.Testing.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
public class TestingController : ControllerBase
{
    private readonly IMediator _mediator;

    public TestingController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpPost("submit")]
    public async Task<IActionResult> SubmitTest([FromBody] SubmitTestRequest request)
    {
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

        // Grade synchronously in-memory using MediatR for all modes in MVP
        await _mediator.Publish(submissionEvent);
        return Ok(new { SessionId = sessionId, Status = "GradedSynchronously" });
    }
}

public record SubmitTestRequest(Guid StudentId, Guid TestId, string TestFormat, Dictionary<Guid, string> Answers);