using SunnySunday.Server.Models;

namespace SunnySunday.Server.Services;

public interface ISchedulerService
{
    Task ScheduleAsync(Settings settings, CancellationToken cancellationToken = default);

    DateTimeOffset? GetNextFireTimeUtc();
}
