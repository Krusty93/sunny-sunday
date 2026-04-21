using System.Data;
using Dapper;
using SunnySunday.Server.Models;

namespace SunnySunday.Server.Data;

public sealed class SettingsRepository(IDbConnection connection)
{
    public async Task<Settings> GetByUserIdAsync(int userId)
    {
        var settings = await connection.QuerySingleOrDefaultAsync<Settings>(
            """
            SELECT
                user_id AS UserId,
                schedule AS Schedule,
                delivery_day AS DeliveryDay,
                delivery_time AS DeliveryTime,
                count AS Count
            FROM settings
            WHERE user_id = @UserId
            """,
            new { UserId = userId });

        return settings ?? new Settings
        {
            UserId = userId
        };
    }

    public Task UpsertAsync(Settings settings)
    {
        return connection.ExecuteAsync(
            """
            INSERT INTO settings (user_id, schedule, delivery_day, delivery_time, count)
            VALUES (@UserId, @Schedule, @DeliveryDay, @DeliveryTime, @Count)
            ON CONFLICT(user_id) DO UPDATE SET
                schedule = excluded.schedule,
                delivery_day = excluded.delivery_day,
                delivery_time = excluded.delivery_time,
                count = excluded.count
            """,
            settings);
    }
}
