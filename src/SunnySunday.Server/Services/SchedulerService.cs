using Quartz;
using SunnySunday.Server.Jobs;
using SunnySunday.Server.Models;

namespace SunnySunday.Server.Services;

public sealed class SchedulerService(
    ISchedulerFactory schedulerFactory,
    ILogger<SchedulerService> logger) : ISchedulerService
{
    private static readonly JobKey RecapJobKey = new("RecapJob", "Recap");
    private static readonly TriggerKey RecapTriggerKey = new("RecapTrigger", "Recap");

    public async Task ScheduleAsync(Settings settings, CancellationToken cancellationToken = default)
    {
        var scheduler = await schedulerFactory.GetScheduler(cancellationToken);

        if (await scheduler.CheckExists(RecapTriggerKey, cancellationToken))
        {
            await scheduler.UnscheduleJob(RecapTriggerKey, cancellationToken);
            logger.LogInformation("Unscheduled existing recap trigger");
        }

        var timeZone = TimeZoneInfo.FindSystemTimeZoneById(settings.Timezone);
        var cronExpression = BuildCronExpression(settings);

        var job = JobBuilder.Create<RecapJob>()
            .WithIdentity(RecapJobKey)
            .StoreDurably()
            .Build();

        var trigger = TriggerBuilder.Create()
            .WithIdentity(RecapTriggerKey)
            .ForJob(RecapJobKey)
            .WithCronSchedule(cronExpression, x => x.InTimeZone(timeZone))
            .Build();

        await scheduler.AddJob(job, replace: true, cancellationToken);
        await scheduler.ScheduleJob(trigger, cancellationToken);

        var nextFire = trigger.GetNextFireTimeUtc();
        logger.LogInformation(
            "Scheduled recap: cron={Cron}, timezone={Tz}, nextFire={NextFire}",
            cronExpression, settings.Timezone, nextFire);
    }

    public DateTimeOffset? GetNextFireTimeUtc()
    {
        var scheduler = schedulerFactory.GetScheduler().GetAwaiter().GetResult();
        var trigger = scheduler.GetTrigger(RecapTriggerKey).GetAwaiter().GetResult();
        return trigger?.GetNextFireTimeUtc();
    }

    internal static string BuildCronExpression(Settings settings)
    {
        var timeParts = settings.DeliveryTime.Split(':');
        var hour = timeParts[0];
        var minute = timeParts[1];

        if (settings.Schedule == "weekly" && !string.IsNullOrWhiteSpace(settings.DeliveryDay))
        {
            var dayOfWeek = NormalizeDayOfWeek(settings.DeliveryDay);
            return $"0 {minute} {hour} ? * {dayOfWeek}";
        }

        return $"0 {minute} {hour} * * ?";
    }

    private static string NormalizeDayOfWeek(string day)
    {
        return day.ToUpperInvariant() switch
        {
            "MONDAY" => "MON",
            "TUESDAY" => "TUE",
            "WEDNESDAY" => "WED",
            "THURSDAY" => "THU",
            "FRIDAY" => "FRI",
            "SATURDAY" => "SAT",
            "SUNDAY" => "SUN",
            _ => day.ToUpperInvariant()[..3]
        };
    }
}
