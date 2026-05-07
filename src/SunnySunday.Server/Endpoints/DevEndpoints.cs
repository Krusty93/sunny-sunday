using Microsoft.AspNetCore.Mvc;
using SunnySunday.Server.Data;
using SunnySunday.Server.Services;

namespace SunnySunday.Server.Endpoints;

/// <summary>
/// Development-only endpoints. Registered exclusively when the host environment is Development.
/// </summary>
public static class DevEndpoints
{
    public static WebApplication MapDevEndpoints(this WebApplication app)
    {
        app.MapPost("/dev/recap/trigger", async (
            [FromServices] UserRepository userRepo,
            [FromServices] IRecapService recapService,
            CancellationToken ct) =>
        {
            var userId = await userRepo.EnsureUserAsync();
            var scheduledFor = DateTimeOffset.UtcNow;

            try
            {
                await recapService.ExecuteAsync(userId, scheduledFor, ct);
                return Results.Ok(new { status = "triggered", scheduledFor });
            }
            catch (Exception ex)
            {
                return Results.Problem(detail: ex.Message, statusCode: StatusCodes.Status500InternalServerError);
            }
        })
        .WithSummary("Trigger a recap immediately (Development only).")
        .WithDescription("Executes the full recap pipeline synchronously for the implicit user. Only available in the Development environment.")
        .Produces(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status500InternalServerError);

        return app;
    }
}
