using System.Data;
using Dapper;
using SunnySunday.Core.Contracts;

namespace SunnySunday.Server.Data;

public sealed class StatusRepository(IDbConnection connection)
{
    public async Task<StatusResponse> GetStatusAsync(int userId)
    {
        var totalHighlights = await connection.QuerySingleAsync<int>(
            "SELECT COUNT(*) FROM highlights WHERE user_id = @UserId",
            new { UserId = userId });

        var totalBooks = await connection.QuerySingleAsync<int>(
            "SELECT COUNT(*) FROM books WHERE user_id = @UserId",
            new { UserId = userId });

        var totalAuthors = await connection.QuerySingleAsync<int>(
            "SELECT COUNT(DISTINCT author_id) FROM books WHERE user_id = @UserId",
            new { UserId = userId });

        var excludedHighlights = await connection.QuerySingleAsync<int>(
            "SELECT COUNT(*) FROM highlights WHERE user_id = @UserId AND excluded = 1",
            new { UserId = userId });

        var excludedBooks = await connection.QuerySingleAsync<int>(
            "SELECT COUNT(*) FROM excluded_books WHERE user_id = @UserId",
            new { UserId = userId });

        var excludedAuthors = await connection.QuerySingleAsync<int>(
            "SELECT COUNT(*) FROM excluded_authors WHERE user_id = @UserId",
            new { UserId = userId });

        return new StatusResponse
        {
            TotalHighlights = totalHighlights,
            TotalBooks = totalBooks,
            TotalAuthors = totalAuthors,
            ExcludedHighlights = excludedHighlights,
            ExcludedBooks = excludedBooks,
            ExcludedAuthors = excludedAuthors,
            NextRecap = null
        };
    }
}
