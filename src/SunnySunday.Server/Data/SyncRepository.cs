using System.Data;
using Dapper;
using SunnySunday.Core.Contracts;

namespace SunnySunday.Server.Data;

public sealed class SyncRepository(IDbConnection connection)
{
    public async Task<SyncResponse> ImportAsync(int userId, SyncRequest request)
    {
        var response = new SyncResponse();

        using var transaction = connection.BeginTransaction();

        foreach (var book in request.Books)
        {
            var authorName = string.IsNullOrWhiteSpace(book.Author) ? "Unknown Author" : book.Author;

            var authorId = await connection.QuerySingleOrDefaultAsync<int?>(
                "SELECT id FROM authors WHERE name = @Name",
                new { Name = authorName }, transaction);

            if (authorId is null)
            {
                await connection.ExecuteAsync(
                    "INSERT INTO authors (name) VALUES (@Name)",
                    new { Name = authorName }, transaction);
                authorId = (int)await connection.ExecuteScalarAsync<long>(
                    "SELECT last_insert_rowid()", transaction: transaction);
                response.NewAuthors++;
            }

            var bookId = await connection.QuerySingleOrDefaultAsync<int?>(
                "SELECT id FROM books WHERE user_id = @UserId AND author_id = @AuthorId AND title = @Title",
                new { UserId = userId, AuthorId = authorId, Title = book.Title }, transaction);

            if (bookId is null)
            {
                await connection.ExecuteAsync(
                    "INSERT INTO books (user_id, author_id, title) VALUES (@UserId, @AuthorId, @Title)",
                    new { UserId = userId, AuthorId = authorId, Title = book.Title }, transaction);
                bookId = (int)await connection.ExecuteScalarAsync<long>(
                    "SELECT last_insert_rowid()", transaction: transaction);
                response.NewBooks++;
            }

            foreach (var highlight in book.Highlights)
            {
                var exists = await connection.QuerySingleOrDefaultAsync<int?>(
                    "SELECT 1 FROM highlights WHERE user_id = @UserId AND book_id = @BookId AND text = @Text",
                    new { UserId = userId, BookId = bookId, Text = highlight.Text }, transaction);

                if (exists.HasValue)
                {
                    response.DuplicateHighlights++;
                    continue;
                }

                await connection.ExecuteAsync(
                    """
                    INSERT INTO highlights (user_id, book_id, text, weight, excluded, delivery_count, created_at)
                    VALUES (@UserId, @BookId, @Text, 3, 0, 0, @CreatedAt)
                    """,
                    new
                    {
                        UserId = userId,
                        BookId = bookId,
                        Text = highlight.Text,
                        CreatedAt = (highlight.AddedOn ?? DateTimeOffset.UtcNow).ToString("o")
                    }, transaction);
                response.NewHighlights++;
            }
        }

        transaction.Commit();
        return response;
    }
}
