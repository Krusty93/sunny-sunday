using System.Net.Http.Json;
using SunnySunday.Core.Contracts;

namespace SunnySunday.Cli.Infrastructure;

/// <summary>
/// Typed HTTP client for all Sunny Sunday server API calls.
/// Uses source-generated JSON context for trimming compatibility.
/// </summary>
public sealed class SunnyHttpClient(HttpClient http)
{
    public async Task<HighlightsResponse> GetHighlightsAsync(int page, int pageSize, string? query, CancellationToken ct = default)
    {
        var requestUri = $"/highlights?page={page}&pageSize={pageSize}";
        if (!string.IsNullOrWhiteSpace(query))
        {
            requestUri += $"&q={Uri.EscapeDataString(query)}";
        }

        return (await http.GetFromJsonAsync(requestUri, SunnyJsonContext.Default.HighlightsResponse, ct).ConfigureAwait(false))!;
    }

    public async Task<SyncResponse> PostSyncAsync(SyncRequest request, CancellationToken ct = default)
    {
        var response = await http.PostAsJsonAsync("/sync", request, SunnyJsonContext.Default.SyncRequest, ct).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync(SunnyJsonContext.Default.SyncResponse, ct).ConfigureAwait(false))!;
    }

    public async Task<SettingsResponse> GetSettingsAsync(CancellationToken ct = default)
        => (await http.GetFromJsonAsync("/settings", SunnyJsonContext.Default.SettingsResponse, ct).ConfigureAwait(false))!;

    public async Task<SettingsResponse> PutSettingsAsync(UpdateSettingsRequest request, CancellationToken ct = default)
    {
        var response = await http.PutAsJsonAsync("/settings", request, SunnyJsonContext.Default.UpdateSettingsRequest, ct).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync(SunnyJsonContext.Default.SettingsResponse, ct).ConfigureAwait(false))!;
    }

    public async Task<StatusResponse> GetStatusAsync(CancellationToken ct = default)
        => (await http.GetFromJsonAsync("/status", SunnyJsonContext.Default.StatusResponse, ct).ConfigureAwait(false))!;

    public async Task<HttpResponseMessage> PostExcludeAsync(string type, int id, CancellationToken ct = default)
    {
        var response = await http.PostAsync($"/{type}s/{id}/exclude", null, ct).ConfigureAwait(false);
        return response;
    }

    public async Task<HttpResponseMessage> DeleteExcludeAsync(string type, int id, CancellationToken ct = default)
    {
        var response = await http.DeleteAsync($"/{type}s/{id}/exclude", ct).ConfigureAwait(false);
        return response;
    }

    public async Task<ExclusionsResponse> GetExclusionsAsync(CancellationToken ct = default)
        => (await http.GetFromJsonAsync("/exclusions", SunnyJsonContext.Default.ExclusionsResponse, ct).ConfigureAwait(false))!;

    public Task<HttpResponseMessage> PostTestEmailAsync(CancellationToken ct = default)
        => http.PostAsync("/settings/test-email", null, ct);

    public Task<HttpResponseMessage> PostTestRecapAsync(CancellationToken ct = default)
        => http.PostAsync("/dev/recap/trigger", null, ct);

    public async Task<bool> PingAsync(CancellationToken ct = default)
    {
        using var response = await http.GetAsync("/", ct).ConfigureAwait(false);
        return response.IsSuccessStatusCode;
    }

    public async Task<HttpResponseMessage> PutWeightAsync(int highlightId, SetWeightRequest request, CancellationToken ct = default)
    {
        var response = await http.PutAsJsonAsync($"/highlights/{highlightId}/weight", request, SunnyJsonContext.Default.SetWeightRequest, ct).ConfigureAwait(false);
        return response;
    }

    public Task<HttpResponseMessage> DeleteHighlightAsync(int id, CancellationToken ct = default)
        => http.DeleteAsync($"/highlights/{id}", ct);

    public async Task<List<WeightedHighlightDto>> GetWeightsAsync(CancellationToken ct = default)
        => (await http.GetFromJsonAsync("/highlights/weights", SunnyJsonContext.Default.ListWeightedHighlightDto, ct).ConfigureAwait(false))!;
}
