using System.Globalization;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc;
using SunnySunday.Core.Contracts;
using SunnySunday.Server.Data;
using SunnySunday.Server.Models;

namespace SunnySunday.Server.Endpoints;

public static partial class SettingsEndpoints
{
    public static WebApplication MapSettingsEndpoints(this WebApplication app)
    {
        app.MapGet("/settings", async ([FromServices] UserRepository userRepo, [FromServices] SettingsRepository settingsRepo) =>
        {
            var userId = await userRepo.EnsureUserAsync();
            var user = await userRepo.GetByIdAsync(userId);
            var settings = await settingsRepo.GetByUserIdAsync(userId);
            return Results.Ok(ToSettingsResponse(user, settings));
        })
        .WithSummary("Read current user settings.")
        .WithDescription("Returns stored settings for the implicit MVP user, or default values when no settings row exists yet.")
        .Produces<SettingsResponse>(StatusCodes.Status200OK);

        app.MapPut("/settings", async (UpdateSettingsRequest? request, [FromServices] UserRepository userRepo, [FromServices] SettingsRepository settingsRepo) =>
        {
            if (request is null)
            {
                return Results.ValidationProblem(
                    new Dictionary<string, string[]> { { "body", ["Request body must not be null."] } },
                    statusCode: StatusCodes.Status422UnprocessableEntity);
            }

            var errors = new Dictionary<string, string[]>();
            string? normalizedSchedule = null;
            string? normalizedDeliveryTime = null;
            string? normalizedKindleEmail = null;

            if (request.Schedule is not null && !IsValidSchedule(request.Schedule, out normalizedSchedule))
                errors["schedule"] = ["Schedule must be either 'daily' or 'weekly'."];

            if (request.DeliveryTime is not null && !IsValidDeliveryTime(request.DeliveryTime, out normalizedDeliveryTime))
                errors["deliveryTime"] = ["Delivery time must use HH:mm format."];

            if (request.Count is < 1 or > 15)
                errors["count"] = ["Count must be between 1 and 15."];

            if (request.KindleEmail is not null && !IsValidEmail(request.KindleEmail, out normalizedKindleEmail))
                errors["kindleEmail"] = ["Invalid email format."];

            if (errors.Count > 0)
                return Results.ValidationProblem(errors, statusCode: StatusCodes.Status422UnprocessableEntity);

            var userId = await userRepo.EnsureUserAsync();
            var user = await userRepo.GetByIdAsync(userId);
            var settings = await settingsRepo.GetByUserIdAsync(userId);

            ApplySettingsUpdate(request, settings, user, normalizedSchedule, normalizedDeliveryTime, normalizedKindleEmail);

            await userRepo.UpdateKindleEmailAsync(user.Id, user.KindleEmail);
            await settingsRepo.UpsertAsync(settings);

            return Results.Ok(ToSettingsResponse(user, settings));
        })
        .WithSummary("Update current user settings.")
        .WithDescription("Applies a partial update to the implicit MVP user and returns the fully resolved settings payload.")
        .Accepts<UpdateSettingsRequest>("application/json")
        .Produces<SettingsResponse>(StatusCodes.Status200OK)
        .ProducesValidationProblem(StatusCodes.Status422UnprocessableEntity);

        return app;
    }

    private static void ApplySettingsUpdate(
        UpdateSettingsRequest request,
        Settings settings,
        User user,
        string? normalizedSchedule,
        string? normalizedDeliveryTime,
        string? normalizedKindleEmail)
    {
        settings.Schedule = normalizedSchedule ?? settings.Schedule;
        settings.DeliveryDay = request.DeliveryDay is null ? settings.DeliveryDay : NormalizeDeliveryDay(request.DeliveryDay);
        settings.DeliveryTime = normalizedDeliveryTime ?? settings.DeliveryTime;
        settings.Count = request.Count ?? settings.Count;
        user.KindleEmail = normalizedKindleEmail ?? user.KindleEmail;
    }

    private static SettingsResponse ToSettingsResponse(User user, Settings settings)
    {
        return new SettingsResponse
        {
            Schedule = settings.Schedule,
            DeliveryDay = settings.DeliveryDay,
            DeliveryTime = settings.DeliveryTime,
            Count = settings.Count,
            KindleEmail = user.KindleEmail
        };
    }

    private static bool IsValidSchedule(string value, out string normalized)
    {
        normalized = value.Trim().ToLowerInvariant();
        return normalized is "daily" or "weekly";
    }

    private static bool IsValidDeliveryTime(string value, out string normalized)
    {
        normalized = value.Trim();
        return TimeOnly.TryParseExact(normalized, "HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out _);
    }

    private static bool IsValidEmail(string value, out string normalized)
    {
        normalized = value.Trim();

        if (string.IsNullOrWhiteSpace(normalized))
            return false;

        return EmailRegex().IsMatch(normalized);
    }

    [GeneratedRegex("^[A-Z0-9.!#$%&'*+/=?^_`{|}~-]+@[A-Z0-9](?:[A-Z0-9-]{0,61}[A-Z0-9])?(?:\\.[A-Z0-9](?:[A-Z0-9-]{0,61}[A-Z0-9])?)+$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex EmailRegex();

    private static string? NormalizeDeliveryDay(string value)
    {
        var normalized = value.Trim();
        return string.IsNullOrWhiteSpace(normalized)
            ? null
            : normalized.ToLowerInvariant();
    }
}