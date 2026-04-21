using Microsoft.AspNetCore.Mvc;
using SunnySunday.Core.Contracts;
using SunnySunday.Server.Data;

namespace SunnySunday.Server.Endpoints;

public static class StatusEndpoints
{
    public static WebApplication MapStatusEndpoints(this WebApplication app)
    {
        app.MapGet("/status", async ([FromServices] UserRepository userRepo, [FromServices] StatusRepository statusRepo) =>
        {
            var userId = await userRepo.EnsureUserAsync();
            var status = await statusRepo.GetStatusAsync(userId);
            return Results.Ok(status);
        })
        .WithSummary("Get server status.")
        .WithDescription("Returns aggregate counts for highlights, books, authors, and exclusions for the implicit MVP user, along with the next scheduled recap time.")
        .Produces<StatusResponse>(StatusCodes.Status200OK);

        return app;
    }
}
