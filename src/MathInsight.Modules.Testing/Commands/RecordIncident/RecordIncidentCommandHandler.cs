using MathInsight.Modules.Testing.Commands.ForceSubmitSession;
using MathInsight.Modules.Testing.Contracts;
using MathInsight.Modules.Testing.Entities;
using MathInsight.Modules.Testing.Errors;
using MathInsight.Modules.Testing.Persistence;
using MathInsight.Shared.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace MathInsight.Modules.Testing.Commands.RecordIncident;

public sealed class RecordIncidentCommandHandler
    : IRequestHandler<RecordIncidentCommand, Result<RecordIncidentResponse>>
{
    private readonly TestingDbContext _db;
    private readonly IMediator _mediator;

    public RecordIncidentCommandHandler(TestingDbContext db, IMediator mediator)
    {
        _db = db;
        _mediator = mediator;
    }

    public async Task<Result<RecordIncidentResponse>> Handle(
        RecordIncidentCommand request,
        CancellationToken cancellationToken)
    {
        // 1. Validate incident type
        if (request.Type is not ("TAB_SWITCH" or "FOCUS_LOSS"))
            return Result<RecordIncidentResponse>.Failure(TestingErrors.InvalidIncidentType);

        // 2. Validate session exists, belongs to student, and is InProgress
        var session = await _db.TestSessions
            .FirstOrDefaultAsync(s => s.SessionId == request.SessionId, cancellationToken);

        if (session is null)
            return Result<RecordIncidentResponse>.Failure(TestingErrors.SessionNotFound);

        if (session.StudentId != request.StudentId)
            return Result<RecordIncidentResponse>.Failure(TestingErrors.SessionNotFound);

        if (session.Status != "InProgress")
            return Result<RecordIncidentResponse>.Failure(TestingErrors.SessionNotInProgress);

        // 3. Insert TestIncident record
        var incident = new TestIncident
        {
            IncidentId = Guid.NewGuid().ToString(),
            SessionId = request.SessionId,
            Type = request.Type,
            Time = DateTime.UtcNow
        };

        _db.TestIncidents.Add(incident);
        await _db.SaveChangesAsync(cancellationToken);

        // 4. Count incidents for session
        var totalIncidents = await _db.TestIncidents
            .CountAsync(i => i.SessionId == request.SessionId, cancellationToken);

        // 5. BR-10: If >= 5 incidents, force-submit the session
        bool forceSubmitted = false;
        if (totalIncidents >= 5)
        {
            var forceResult = await _mediator.Send(
                new ForceSubmitSessionCommand(request.SessionId, "SystemSubmit"),
                cancellationToken);

            forceSubmitted = forceResult.IsSuccess;
        }

        return Result<RecordIncidentResponse>.Success(
            new RecordIncidentResponse(
                IncidentId: incident.IncidentId,
                TotalIncidents: totalIncidents,
                ForceSubmitted: forceSubmitted));
    }
}
