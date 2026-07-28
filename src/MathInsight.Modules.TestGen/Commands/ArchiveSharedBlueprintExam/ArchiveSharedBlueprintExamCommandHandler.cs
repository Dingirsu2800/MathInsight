using MathInsight.Modules.TestGen.Commands.Common;
using MathInsight.Modules.TestGen.Commands.GenerateBlueprintExam;
using MathInsight.Modules.TestGen.Contracts.Tests;
using MathInsight.Modules.TestGen.Errors;
using MathInsight.Modules.TestGen.Generation;
using MathInsight.Modules.TestGen.Persistence;
using MathInsight.Shared.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace MathInsight.Modules.TestGen.Commands.ArchiveSharedBlueprintExam;

public sealed class ArchiveSharedBlueprintExamCommandHandler
    : IRequestHandler<ArchiveSharedBlueprintExamCommand, Result<UpdateGeneratedTestStatusResponse>>
{
    private readonly TestGenDbContext _context;

    public ArchiveSharedBlueprintExamCommandHandler(TestGenDbContext context)
    {
        _context = context;
    }

    public async Task<Result<UpdateGeneratedTestStatusResponse>> Handle(
        ArchiveSharedBlueprintExamCommand command,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(command.ExpertId))
            return Result<UpdateGeneratedTestStatusResponse>.Failure(ApplicationErrors.AuthInvalidToken);
        if (string.IsNullOrWhiteSpace(command.TestId) ||
            command.Status != GeneratedTestValues.ArchivedStatus)
        {
            return Result<UpdateGeneratedTestStatusResponse>.Failure(TestGenerationErrors.RequestInvalid);
        }

        return await TestGenerationExecutionStrategy.ExecuteAsync(
            _context,
            () => ExecuteAsync(command, cancellationToken),
            () => VerifySucceededAsync(command, cancellationToken),
            cancellationToken);
    }

    private async Task<Result<UpdateGeneratedTestStatusResponse>> ExecuteAsync(
        ArchiveSharedBlueprintExamCommand command,
        CancellationToken cancellationToken)
    {

        await using IDbContextTransaction? transaction = _context.Database.IsRelational()
            ? await _context.Database.BeginTransactionAsync(cancellationToken)
            : null;
        await TestSqlServerLock.LockAsync(_context, command.TestId, cancellationToken);

        var test = await _context.Tests
            .Include(item => item.Blueprint)
            .FirstOrDefaultAsync(item => item.TestId == command.TestId, cancellationToken);
        if (test is null ||
            test.TestMode != GeneratedTestValues.BlueprintExamMode ||
            test.GeneratedForStudentId is not null ||
            test.Blueprint is null)
        {
            return Result<UpdateGeneratedTestStatusResponse>.Failure(TestGenerationErrors.GeneratedTestNotFound);
        }

        if (!string.Equals(test.Blueprint.ExpertId, command.ExpertId, StringComparison.OrdinalIgnoreCase))
            return Result<UpdateGeneratedTestStatusResponse>.Failure(BlueprintErrors.MutationForbidden);

        if (test.TestStatus == GeneratedTestValues.ArchivedStatus)
            return Result<UpdateGeneratedTestStatusResponse>.Success(new(test.TestId, test.TestStatus));
        if (test.TestStatus != GeneratedTestValues.ActiveStatus)
            return Result<UpdateGeneratedTestStatusResponse>.Failure(TestGenerationErrors.GeneratedTestNotFound);

        test.TestStatus = GeneratedTestValues.ArchivedStatus;
        await _context.SaveChangesAsync(cancellationToken);
        if (transaction is not null)
            await transaction.CommitAsync(cancellationToken);

        return Result<UpdateGeneratedTestStatusResponse>.Success(new(test.TestId, test.TestStatus));
    }

    private async Task<(bool IsSuccessful, Result<UpdateGeneratedTestStatusResponse> Result)> VerifySucceededAsync(
        ArchiveSharedBlueprintExamCommand command,
        CancellationToken cancellationToken)
    {
        var test = await _context.Tests
            .AsNoTracking()
            .Include(item => item.Blueprint)
            .FirstOrDefaultAsync(item => item.TestId == command.TestId, cancellationToken);
        var succeeded = test is not null &&
            test.TestMode == GeneratedTestValues.BlueprintExamMode &&
            test.GeneratedForStudentId is null &&
            test.Blueprint is not null &&
            string.Equals(test.Blueprint.ExpertId, command.ExpertId, StringComparison.OrdinalIgnoreCase) &&
            test.TestStatus == GeneratedTestValues.ArchivedStatus;

        return succeeded
            ? (true, Result<UpdateGeneratedTestStatusResponse>.Success(new(test!.TestId, test.TestStatus)))
            : (false, default!);
    }
}
