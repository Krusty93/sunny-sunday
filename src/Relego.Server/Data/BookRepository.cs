using System.Data;
using Dapper;

namespace Relego.Server.Data;

public sealed class BookRepository(IDbConnection connection)
{
    /// <summary>
    /// Renames a book title.
    /// Returns <see langword="true"/> when the book was found and updated,
    /// <see langword="false"/> when no book with <paramref name="bookId"/> belongs to <paramref name="userId"/>,
    /// and <see langword="null"/> when the new title already exists for the same author and user.
    /// </summary>
    public async Task<bool?> RenameAsync(int userId, int bookId, string newTitle)
    {
        // Verify the book exists and belongs to the user
        var authorId = await connection.QuerySingleOrDefaultAsync<int?>(
            "SELECT author_id FROM books WHERE id = @BookId AND user_id = @UserId",
            new { BookId = bookId, UserId = userId }).ConfigureAwait(false);

        if (authorId is null)
            return false;

        // Check for a duplicate title under the same author
        var duplicate = await connection.ExecuteScalarAsync<int>(
            """
            SELECT COUNT(*) FROM books
            WHERE user_id = @UserId AND author_id = @AuthorId AND title = @Title AND id != @BookId
            """,
            new { UserId = userId, AuthorId = authorId.Value, Title = newTitle, BookId = bookId })
            .ConfigureAwait(false);

        if (duplicate > 0)
            return null;

        await connection.ExecuteAsync(
            "UPDATE books SET title = @Title WHERE id = @BookId AND user_id = @UserId",
            new { Title = newTitle, BookId = bookId, UserId = userId })
            .ConfigureAwait(false);

        return true;
    }
}
