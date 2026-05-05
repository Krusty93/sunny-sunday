using Microsoft.AspNetCore.Mvc;
using SunnySunday.Core.Contracts;
using SunnySunday.Server.Data;

namespace SunnySunday.Server.Endpoints;

public static class HighlightEndpoints
{
    public static WebApplication MapHighlightEndpoints(this WebApplication app)
    {
        app.MapGet("/highlights", async (
            [FromServices] UserRepository userRepo,
            [FromServices] HighlightRepository highlightRepo,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 50,
            [FromQuery] string? q = null) =>
        {
            if (page < 1)
            {
                return Results.ValidationProblem(
                    new Dictionary<string, string[]> { { "page", ["page must be greater than or equal to 1."] } },
                    statusCode: StatusCodes.Status422UnprocessableEntity);
            }

            if (pageSize is < 1 or > 200)
            {
                return Results.ValidationProblem(
                    new Dictionary<string, string[]> { { "pageSize", ["pageSize must be between 1 and 200."] } },
                    statusCode: StatusCodes.Status422UnprocessableEntity);
            }

            var userId = await userRepo.EnsureUserAsync();
            var result = await highlightRepo.GetHighlightsAsync(userId, page, pageSize, q);
            return Results.Ok(result);
        })
        .WithSummary("List highlights.")
        .WithDescription("Returns a paginated, optionally filtered list of highlights stored in the database.")
        .Produces<HighlightsResponse>(StatusCodes.Status200OK)
        .ProducesValidationProblem(StatusCodes.Status422UnprocessableEntity);

        return app;
    }
}
