using System.Data;
using Dapper;

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
}
