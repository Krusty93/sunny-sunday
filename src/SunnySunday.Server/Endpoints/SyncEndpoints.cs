using Microsoft.AspNetCore.Mvc;
using SunnySunday.Core.Contracts;
using SunnySunday.Server.Data;

namespace SunnySunday.Server.Endpoints;

public static class SyncEndpoints
{
    public static WebApplication MapSyncEndpoints(this WebApplication app)
    {
        app.MapPost("/sync", async (SyncRequest? request, [FromServices] UserRepository userRepo, [FromServices] SyncRepository syncRepo) =>
        {
            if (request is null || request.Books is null)
                return Results.ValidationProblem(
                    new Dictionary<string, string[]> { { "books", ["Books must not be null."] } },
                    statusCode: StatusCodes.Status422UnprocessableEntity);

            var errors = new Dictionary<string, string[]>();

            for (var i = 0; i < request.Books.Count; i++)
            {
                var book = request.Books[i];

                if (string.IsNullOrWhiteSpace(book.Title))
                    errors[$"books[{i}].title"] = ["Book title must not be empty."];

                if (book.Highlights is null || book.Highlights.Count == 0)
                {
                    errors[$"books[{i}].highlights"] = ["Book must have at least one highlight."];
                }
                else
                {
                    for (var j = 0; j < book.Highlights.Count; j++)
                    {
                        if (string.IsNullOrWhiteSpace(book.Highlights[j].Text))
                            errors[$"books[{i}].highlights[{j}].text"] = ["Highlight text must not be empty or whitespace."];
                    }
                }
            }

            if (errors.Count > 0)
                return Results.ValidationProblem(errors, statusCode: StatusCodes.Status422UnprocessableEntity);

            var userId = await userRepo.EnsureUserAsync();
            var response = await syncRepo.ImportAsync(userId, request);
            return Results.Ok(response);
        });

        return app;
    }
}
