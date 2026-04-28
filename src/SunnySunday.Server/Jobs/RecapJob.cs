using Quartz;
using SunnySunday.Server.Data;
using SunnySunday.Server.Services;

namespace SunnySunday.Server.Jobs;

[DisallowConcurrentExecution]
public sealed class RecapJob(
    IServiceScopeFactory scopeFactory,
    ILogger<RecapJob> logger) : IJob
{
    // MVP: single-user, always user 1
    private const int UserId = 1;

    public async Task Execute(IJobExecutionContext context)
    {
        var scheduledFor = context.ScheduledFireTimeUtc ?? context.FireTimeUtc;

        await using var scope = scopeFactory.CreateAsyncScope();
        var recapRepository = scope.ServiceProvider.GetRequiredService<RecapRepository>();

        var existingJob = await recapRepository.GetJobBySlotAsync(UserId, scheduledFor);
        if (existingJob is { Status: "delivered" })
        {
            logger.LogInformation("Recap slot {ScheduledFor} already delivered for user {UserId}, skipping", scheduledFor, UserId);
            return;
        }

        var recapService = scope.ServiceProvider.GetRequiredService<IRecapService>();
        await recapService.ExecuteAsync(UserId, scheduledFor, context.CancellationToken);
    }
}
