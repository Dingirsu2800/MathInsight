using MathInsight.Modules.Testing.Contracts;
using MathInsight.Modules.Testing.Entities;
using MathInsight.Modules.Testing.Errors;
using MathInsight.Modules.Testing.Persistence;
using MathInsight.Shared.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace MathInsight.Modules.Testing.Commands.AutoSave;

public sealed class AutoSaveCommandHandler
    : IRequestHandler<AutoSaveCommand, Result<AutoSaveResponse>>
{
    private readonly TestingDbContext _db;

    public AutoSaveCommandHandler(TestingDbContext db)
    {
        _db = db;
    }

    public async Task<Result<AutoSaveResponse>> Handle(
        AutoSaveCommand request,
        CancellationToken cancellationToken)
    {
        // 1. Validate session exists, belongs to student, and is InProgress
        var session = await _db.TestSessions
            .Include(s => s.Test)
            .FirstOrDefaultAsync(s => s.SessionId == request.SessionId, cancellationToken);

        if (session is null)
            return Result<AutoSaveResponse>.Failure(TestingErrors.SessionNotFound);

        if (session.StudentId != request.StudentId)
            return Result<AutoSaveResponse>.Failure(TestingErrors.SessionNotFound);

        if (session.Status != "InProgress")
            return Result<AutoSaveResponse>.Failure(TestingErrors.SessionNotInProgress);

        // 2. Load existing answers for this session
        var existingAnswers = await _db.TestAnswers
            .Include(a => a.Options)
            .Include(a => a.Parts)
            .Where(a => a.SessionId == request.SessionId)
            .ToListAsync(cancellationToken);

        var answerLookup = existingAnswers.ToDictionary(a => a.QuestionId);
        var snapshots = await QuestionSnapshotReader.LoadAsync(
            _db,
            session.TestId,
            cancellationToken);
        var now = DateTime.UtcNow;

        // 3. Batch update answers
        foreach (var dto in request.Answers)
        {
            if (!answerLookup.TryGetValue(dto.QuestionId, out var answer) ||
                !snapshots.TryGetValue(dto.QuestionId, out var snapshot) ||
                !QuestionSnapshotReader.IsValid(snapshot, dto))
            {
                return Result<AutoSaveResponse>.Failure(TestingErrors.AnswerNotInVersion);
            }

            // Update basic answer fields
            answer.AnswerId = dto.AnswerId;
            answer.ShortAnswerText = dto.ShortAnswerText;
            answer.TimeSpent = dto.TimeSpent;
            answer.UpdateChoiceTime = now;

            // Set FirstChoiceTime if null (first time answering)
            answer.FirstChoiceTime ??= now;

            // Update selected options (MULTIPLE_SELECT)
            if (dto.SelectedOptions is not null)
            {
                // Remove existing options
                _db.TestAnswerOptions.RemoveRange(answer.Options);

                // Add new options
                foreach (var opt in dto.SelectedOptions)
                {
                    _db.TestAnswerOptions.Add(new TestAnswerOption
                    {
                        TestAnswerId = answer.TestAnswerId,
                        AnswerId = opt.AnswerId
                    });
                }
            }

            // Update answer parts (COMPOSITE)
            if (dto.Parts is not null)
            {
                foreach (var partDto in dto.Parts)
                {
                    var existingPart = answer.Parts
                        .FirstOrDefault(p => p.PartId == partDto.PartId);

                    if (existingPart is not null)
                    {
                        existingPart.BooleanAnswer = partDto.BooleanAnswer;
                        existingPart.TextAnswer = partDto.TextAnswer;
                        existingPart.NumericAnswer = partDto.NumericAnswer;
                    }
                    else
                    {
                        _db.TestAnswerParts.Add(new TestAnswerPart
                        {
                            TestAnswerId = answer.TestAnswerId,
                            PartId = partDto.PartId,
                            BooleanAnswer = partDto.BooleanAnswer,
                            TextAnswer = partDto.TextAnswer,
                            NumericAnswer = partDto.NumericAnswer,
                            PointsEarned = 0
                        });
                    }
                }
            }
        }

        await _db.SaveChangesAsync(cancellationToken);

        // 4. Calculate remaining time
        var durationMinutes = session.Test?.DurationMinutes ?? 0;
        var elapsed = (now - session.StartTime).TotalSeconds;
        var remainingSeconds = Math.Max(0, (int)(durationMinutes * 60 - elapsed));

        return Result<AutoSaveResponse>.Success(
            new AutoSaveResponse(SavedAt: now, RemainingSeconds: remainingSeconds));
    }
}
