using System.Data;
using Dapper;
using Microsoft.AspNetCore.Mvc;

namespace Relego.Server.Endpoints;

public static class ProbeEndpoints
{
    public static WebApplication MapProbeEndpoints(this WebApplication app)
    {
        app.MapGet("/healthz/live", async ([FromServices] IDbConnection db) =>
        {
            try
            {
                await db.ExecuteScalarAsync<int>("SELECT 1").ConfigureAwait(false);
                return Results.NoContent();
            }
            catch
            {
                return Results.StatusCode(StatusCodes.Status503ServiceUnavailable);
            }
        })
        .WithSummary("Liveness probe.")
        .WithDescription("Verifies database connectivity. Returns 204 when healthy; 503 when the database is unreachable. Intended for container liveness probes.")
        .Produces(StatusCodes.Status204NoContent)
        .Produces(StatusCodes.Status503ServiceUnavailable)
        .ExcludeFromDescription();

        app.MapGet("/healthz/startup", async ([FromServices] IDbConnection db) =>
        {
            try
            {
                await db.ExecuteScalarAsync<int>("SELECT 1").ConfigureAwait(false);
                return Results.NoContent();
            }
            catch
            {
                return Results.StatusCode(StatusCodes.Status503ServiceUnavailable);
            }
        })
        .WithSummary("Startup probe.")
        .WithDescription("Verifies that the server has finished initializing. Returns 204 when ready; 503 when not yet available. Intended for container startup probes.")
        .Produces(StatusCodes.Status204NoContent)
        .Produces(StatusCodes.Status503ServiceUnavailable)
        .ExcludeFromDescription();

        return app;
    }
}
