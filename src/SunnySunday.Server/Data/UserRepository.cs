using System.Data;
using Dapper;
using SunnySunday.Server.Models;

namespace SunnySunday.Server.Data;

public sealed class UserRepository(IDbConnection connection)
{
    public async Task<int> EnsureUserAsync()
    {
        await connection.ExecuteAsync(
            "INSERT OR IGNORE INTO users (id, kindle_email, created_at) VALUES (1, '', @CreatedAt)",
            new { CreatedAt = DateTimeOffset.UtcNow.ToString("o") });

        return 1;
    }

    public async Task<User> GetByIdAsync(int userId)
    {
        var row = await connection.QuerySingleOrDefaultAsync<UserRow>(
            """
            SELECT
                id AS Id,
                kindle_email AS KindleEmail,
                created_at AS CreatedAt
            FROM users
            WHERE id = @UserId
            """,
            new { UserId = userId });

        return row is null
            ? throw new InvalidOperationException($"User {userId} was not found.")
            : new User
            {
                Id = row.Id,
                KindleEmail = row.KindleEmail,
                CreatedAt = DateTimeOffset.Parse(row.CreatedAt, null, System.Globalization.DateTimeStyles.RoundtripKind)
            };
    }

    public Task UpdateKindleEmailAsync(int userId, string kindleEmail)
    {
        return connection.ExecuteAsync(
            "UPDATE users SET kindle_email = @KindleEmail WHERE id = @UserId",
            new { UserId = userId, KindleEmail = kindleEmail });
    }

    private sealed class UserRow
    {
        public int Id { get; init; }
        public string KindleEmail { get; init; } = string.Empty;
        public string CreatedAt { get; init; } = string.Empty;
    }
}
