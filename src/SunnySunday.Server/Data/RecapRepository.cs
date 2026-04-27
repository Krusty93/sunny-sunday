using System.Data;
using Dapper;
using SunnySunday.Server.Models;

namespace SunnySunday.Server.Data;

public sealed class RecapRepository(IDbConnection connection)
{
    public async Task<int> CreateJobAsync(int userId, DateTimeOffset scheduledFor)
    {
        var scheduledForText = scheduledFor.UtcDateTime.ToString("O");
        var createdAtText = DateTimeOffset.UtcNow.ToString("O");

        return await connection.QuerySingleOrDefaultAsync<int>(
            """
            INSERT OR IGNORE INTO recap_jobs (user_id, scheduled_for, created_at)
            VALUES (@UserId, @ScheduledFor, @CreatedAt);
            SELECT id FROM recap_jobs WHERE user_id = @UserId AND scheduled_for = @ScheduledFor
            """,
            new { UserId = userId, ScheduledFor = scheduledForText, CreatedAt = createdAtText });
    }

    public async Task<RecapJobRecord?> GetJobBySlotAsync(int userId, DateTimeOffset scheduledFor)
    {
        var scheduledForText = scheduledFor.UtcDateTime.ToString("O");

        return await connection.QuerySingleOrDefaultAsync<RecapJobRecord>(
            """
            SELECT id AS Id, user_id AS UserId, scheduled_for AS ScheduledFor,
                   status AS Status, attempt_count AS AttemptCount,
                   error_message AS ErrorMessage, created_at AS CreatedAt,
                   delivered_at AS DeliveredAt
            FROM recap_jobs
            WHERE user_id = @UserId AND scheduled_for = @ScheduledFor
            """,
            new { UserId = userId, ScheduledFor = scheduledForText });
    }

    public Task UpdateJobDeliveredAsync(int jobId, DateTimeOffset deliveredAt, int attemptCount)
    {
        return connection.ExecuteAsync(
            """
            UPDATE recap_jobs
            SET status = 'delivered', delivered_at = @DeliveredAt, attempt_count = @AttemptCount
            WHERE id = @JobId
            """,
            new { JobId = jobId, DeliveredAt = deliveredAt.UtcDateTime.ToString("O"), AttemptCount = attemptCount });
    }

    public Task UpdateJobFailedAsync(int jobId, string errorMessage, int attemptCount)
    {
        return connection.ExecuteAsync(
            """
            UPDATE recap_jobs
            SET status = 'failed', error_message = @ErrorMessage, attempt_count = @AttemptCount
            WHERE id = @JobId
            """,
            new { JobId = jobId, ErrorMessage = errorMessage, AttemptCount = attemptCount });
    }

    public async Task<RecapJobRecord?> GetLastJobAsync(int userId)
    {
        return await connection.QuerySingleOrDefaultAsync<RecapJobRecord>(
            """
            SELECT id AS Id, user_id AS UserId, scheduled_for AS ScheduledFor,
                   status AS Status, attempt_count AS AttemptCount,
                   error_message AS ErrorMessage, created_at AS CreatedAt,
                   delivered_at AS DeliveredAt
            FROM recap_jobs
            WHERE user_id = @UserId
            ORDER BY created_at DESC
            LIMIT 1
            """,
            new { UserId = userId });
    }

    public Task UpdateHighlightSeenAsync(int highlightId, DateTimeOffset seenAt)
    {
        return connection.ExecuteAsync(
            """
            UPDATE highlights
            SET last_seen = @SeenAt, delivery_count = delivery_count + 1
            WHERE id = @HighlightId
            """,
            new { HighlightId = highlightId, SeenAt = seenAt.UtcDateTime.ToString("O") });
    }
}
