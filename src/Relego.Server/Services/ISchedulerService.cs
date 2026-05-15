using Relego.Server.Models;

namespace Relego.Server.Services;

public interface ISchedulerService
{
    Task ScheduleAsync(Settings settings, CancellationToken cancellationToken = default);

    DateTimeOffset? GetNextFireTimeUtc();
}
