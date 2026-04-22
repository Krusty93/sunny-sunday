using System.Data;
using Dapper;
using SunnySunday.Core.Contracts;

namespace SunnySunday.Server.Data;

public sealed class ExclusionRepository(IDbConnection connection)
{
    public async Task<bool> ExcludeHighlightAsync(int userId, int highlightId)
    {
        var affectedRows = await connection.ExecuteAsync(
            "UPDATE highlights SET excluded = 1 WHERE id = @HighlightId AND user_id = @UserId",
            new { HighlightId = highlightId, UserId = userId });

        return affectedRows > 0;
    }

    public async Task<bool> IncludeHighlightAsync(int userId, int highlightId)
    {
        var affectedRows = await connection.ExecuteAsync(
            "UPDATE highlights SET excluded = 0 WHERE id = @HighlightId AND user_id = @UserId",
            new { HighlightId = highlightId, UserId = userId });

        return affectedRows > 0;
    }

    public async Task<bool> ExcludeBookAsync(int userId, int bookId)
    {
        if (!await BookExistsAsync(userId, bookId))
            return false;

        await connection.ExecuteAsync(
            """
            INSERT INTO excluded_books (user_id, book_id, excluded_at)
            SELECT @UserId, @BookId, @ExcludedAt
            WHERE NOT EXISTS (
                SELECT 1
                FROM excluded_books
                WHERE user_id = @UserId AND book_id = @BookId
            )
            """,
            new
            {
                UserId = userId,
                BookId = bookId,
                ExcludedAt = DateTimeOffset.UtcNow.ToString("o")
            });

        return true;
    }

    public async Task<bool> IncludeBookAsync(int userId, int bookId)
    {
        if (!await BookExistsAsync(userId, bookId))
            return false;

        await connection.ExecuteAsync(
            "DELETE FROM excluded_books WHERE user_id = @UserId AND book_id = @BookId",
            new { UserId = userId, BookId = bookId });

        return true;
    }

    public async Task<bool> ExcludeAuthorAsync(int userId, int authorId)
    {
        if (!await AuthorExistsForUserAsync(userId, authorId))
            return false;

        await connection.ExecuteAsync(
            """
            INSERT INTO excluded_authors (user_id, author_id, excluded_at)
            SELECT @UserId, @AuthorId, @ExcludedAt
            WHERE NOT EXISTS (
                SELECT 1
                FROM excluded_authors
                WHERE user_id = @UserId AND author_id = @AuthorId
            )
            """,
            new
            {
                UserId = userId,
                AuthorId = authorId,
                ExcludedAt = DateTimeOffset.UtcNow.ToString("o")
            });

        return true;
    }

    public async Task<bool> IncludeAuthorAsync(int userId, int authorId)
    {
        if (!await AuthorExistsForUserAsync(userId, authorId))
            return false;

        await connection.ExecuteAsync(
            "DELETE FROM excluded_authors WHERE user_id = @UserId AND author_id = @AuthorId",
            new { UserId = userId, AuthorId = authorId });

        return true;
    }

    public async Task<ExclusionsResponse> GetExclusionsAsync(int userId)
    {
        var highlights = (await connection.QueryAsync<ExcludedHighlightDto>(
            """
            SELECT
                h.id AS Id,
                SUBSTR(h.text, 1, 100) AS Text,
                b.title AS BookTitle
            FROM highlights h
            INNER JOIN books b ON b.id = h.book_id
            WHERE h.user_id = @UserId
              AND h.excluded = 1
            ORDER BY h.id
            """,
            new { UserId = userId })).AsList();

        var books = (await connection.QueryAsync<ExcludedBookDto>(
            """
            SELECT DISTINCT
                b.id AS Id,
                b.title AS Title,
                a.name AS AuthorName,
                (
                    SELECT COUNT(*)
                    FROM highlights h
                    WHERE h.user_id = @UserId
                      AND h.book_id = b.id
                ) AS HighlightCount
            FROM excluded_books eb
            INNER JOIN books b ON b.id = eb.book_id AND b.user_id = eb.user_id
            INNER JOIN authors a ON a.id = b.author_id
            WHERE eb.user_id = @UserId
            ORDER BY b.id
            """,
            new { UserId = userId })).AsList();

        var authors = (await connection.QueryAsync<ExcludedAuthorDto>(
            """
            SELECT DISTINCT
                a.id AS Id,
                a.name AS Name,
                (
                    SELECT COUNT(*)
                    FROM books b
                    WHERE b.user_id = @UserId
                      AND b.author_id = a.id
                ) AS BookCount
            FROM excluded_authors ea
            INNER JOIN authors a ON a.id = ea.author_id
            WHERE ea.user_id = @UserId
            ORDER BY a.id
            """,
            new { UserId = userId })).AsList();

        return new ExclusionsResponse
        {
            Highlights = highlights,
            Books = books,
            Authors = authors
        };
    }

    private async Task<bool> BookExistsAsync(int userId, int bookId)
    {
        var exists = await connection.QuerySingleOrDefaultAsync<int?>(
            "SELECT 1 FROM books WHERE id = @BookId AND user_id = @UserId",
            new { BookId = bookId, UserId = userId });

        return exists.HasValue;
    }

    private async Task<bool> AuthorExistsForUserAsync(int userId, int authorId)
    {
        var exists = await connection.QuerySingleOrDefaultAsync<int?>(
            "SELECT 1 FROM books WHERE author_id = @AuthorId AND user_id = @UserId LIMIT 1",
            new { AuthorId = authorId, UserId = userId });

        return exists.HasValue;
    }
}
