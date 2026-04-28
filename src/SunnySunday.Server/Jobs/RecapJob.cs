using Quartz;
using SunnySunday.Server.Data;
using SunnySunday.Server.Services;

namespace SunnySunday.Server.Jobs;

[DisallowConcurrentExecution]
public sealed class RecapJob(
    IServiceScopeFactory scopeFactory,
    ILogger<RecapJob> logger) : IJob
{
    public const string UserIdKey = "UserId";

    public async Task Execute(IJobExecutionContext context)
    {
        var userId = context.MergedJobDataMap.GetInt(UserIdKey);
        var scheduledFor = context.ScheduledFireTimeUtc ?? context.FireTimeUtc;

        await using var scope = scopeFactory.CreateAsyncScope();
        var recapRepository = scope.ServiceProvider.GetRequiredService<RecapRepository>();

        var existingJob = await recapRepository.GetJobBySlotAsync(userId, scheduledFor);
        if (existingJob is { Status: "delivered" })
        {
            logger.LogInformation("Recap slot {ScheduledFor} already delivered for user {UserId}, skipping", scheduledFor, userId);
            return;
        }

        var recapService = scope.ServiceProvider.GetRequiredService<IRecapService>();
        await recapService.ExecuteAsync(userId, scheduledFor, context.CancellationToken);
    }
}
