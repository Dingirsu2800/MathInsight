using MathInsight.Shared.Scoring;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace MathInsight.Modules.Grading_Analytics.BackgroundJobs;

/// <summary>
/// Retries delivery of score-adjustment events persisted by <see cref="Services.ScoreAdjustmentService"/>.
/// The work rows are created in the same transaction as the recalculated score, so restart recovery
/// does not depend on a user pressing the retry endpoint.
/// </summary>
public sealed class ScoreAdjustmentDispatchBackgroundService : BackgroundService
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(30);
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<ScoreAdjustmentDispatchBackgroundService> _logger;

    public ScoreAdjustmentDispatchBackgroundService(
        IServiceScopeFactory scopeFactory,
        ILogger<ScoreAdjustmentDispatchBackgroundService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var service = scope.ServiceProvider.GetRequiredService<IScoreAdjustmentService>();
                await service.RecoverPendingAdjustmentsAsync(stoppingToken);
                await service.DispatchPendingAdjustmentsAsync(cancellationToken: stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, "Score adjustment delivery retry failed.");
            }

            try
            {
                await Task.Delay(PollInterval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }
}
