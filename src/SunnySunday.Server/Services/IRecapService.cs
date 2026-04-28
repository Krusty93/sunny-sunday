namespace SunnySunday.Server.Services;

public interface IRecapService
{
    Task ExecuteAsync(int userId, DateTimeOffset scheduledFor, CancellationToken cancellationToken = default);
}
