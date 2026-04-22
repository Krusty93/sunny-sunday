using System.Data;
using Dapper;
using SunnySunday.Core.Contracts;

namespace SunnySunday.Server.Data;

public sealed class WeightRepository(IDbConnection connection)
{
    public async Task<bool> SetWeightAsync(int userId, int highlightId, int weight)
    {
        var affectedRows = await connection.ExecuteAsync(
            "UPDATE highlights SET weight = @Weight WHERE id = @HighlightId AND user_id = @UserId",
            new { Weight = weight, HighlightId = highlightId, UserId = userId });

        return affectedRows > 0;
    }

    public async Task<List<WeightedHighlightDto>> GetWeightedHighlightsAsync(int userId)
    {
        var highlights = await connection.QueryAsync<WeightedHighlightDto>(
            """
            SELECT
                h.id AS Id,
                SUBSTR(h.text, 1, 100) AS Text,
                b.title AS BookTitle,
                h.weight AS Weight
            FROM highlights h
            INNER JOIN books b ON b.id = h.book_id
            WHERE h.user_id = @UserId
              AND h.weight != 3
            ORDER BY h.weight DESC, h.id ASC
            """,
            new { UserId = userId });

        return highlights.AsList();
    }
}
