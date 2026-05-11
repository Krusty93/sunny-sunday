using System.Data;
using Dapper;
using SunnySunday.Core.Contracts;

namespace SunnySunday.Server.Data;

public sealed class HighlightRepository(IDbConnection connection)
{
    public async Task<HighlightsResponse> GetHighlightsAsync(int userId, int page, int pageSize, string? q)
    {
        var hasFilter = !string.IsNullOrWhiteSpace(q);
        var filter = hasFilter ? $"%{q}%" : null;

        var countSql = hasFilter
            ? """
              SELECT COUNT(*)
              FROM highlights h
              INNER JOIN books b ON b.id = h.book_id
              INNER JOIN authors a ON a.id = b.author_id
              WHERE h.user_id = @UserId
                AND (h.text LIKE @Filter OR b.title LIKE @Filter OR a.name LIKE @Filter)
              """
            : "SELECT COUNT(*) FROM highlights WHERE user_id = @UserId";

        var itemSql = hasFilter
            ? """
              SELECT h.id AS Id, b.id AS BookId, a.id AS AuthorId, h.text AS Text, b.title AS BookTitle, a.name AS AuthorName
              FROM highlights h
              INNER JOIN books b ON b.id = h.book_id
              INNER JOIN authors a ON a.id = b.author_id
              WHERE h.user_id = @UserId
                AND (h.text LIKE @Filter OR b.title LIKE @Filter OR a.name LIKE @Filter)
              ORDER BY h.id ASC
              LIMIT @PageSize OFFSET @Offset
              """
            : """
              SELECT h.id AS Id, b.id AS BookId, a.id AS AuthorId, h.text AS Text, b.title AS BookTitle, a.name AS AuthorName
              FROM highlights h
              INNER JOIN books b ON b.id = h.book_id
              INNER JOIN authors a ON a.id = b.author_id
              WHERE h.user_id = @UserId
              ORDER BY h.id ASC
              LIMIT @PageSize OFFSET @Offset
              """;

        var param = new { UserId = userId, Filter = filter, PageSize = pageSize, Offset = (page - 1) * pageSize };

        var total = await connection.ExecuteScalarAsync<int>(countSql, param);
        var items = await connection.QueryAsync<HighlightItemDto>(itemSql, param);

        return new HighlightsResponse
        {
            Total = total,
            Page = page,
            PageSize = pageSize,
            Items = items.AsList()
        };
    }

    public async Task<bool> DeleteHighlightAsync(int userId, int highlightId)
    {
        var affectedRows = await connection.ExecuteAsync(
            "DELETE FROM highlights WHERE id = @HighlightId AND user_id = @UserId",
            new { HighlightId = highlightId, UserId = userId });

        return affectedRows > 0;
    }
}
