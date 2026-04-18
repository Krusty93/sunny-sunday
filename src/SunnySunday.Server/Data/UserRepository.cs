using System.Data;
using Dapper;

namespace SunnySunday.Server.Data;

public sealed class UserRepository(IDbConnection connection)
{
    public async Task<int> EnsureUserAsync()
    {
        var existingId = await connection.QuerySingleOrDefaultAsync<int?>(
            "SELECT id FROM users WHERE id = 1");

        if (existingId.HasValue)
            return existingId.Value;

        await connection.ExecuteAsync(
            "INSERT INTO users (id, kindle_email, created_at) VALUES (1, '', @CreatedAt)",
            new { CreatedAt = DateTimeOffset.UtcNow.ToString("o") });

        return 1;
    }
}
