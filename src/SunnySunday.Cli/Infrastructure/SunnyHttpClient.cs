using System.Net.Http.Json;
using SunnySunday.Core.Contracts;

namespace SunnySunday.Cli.Infrastructure;

/// <summary>
/// Typed HTTP client for all Sunny Sunday server API calls.
/// Uses source-generated JSON context for trimming compatibility.
/// </summary>
public sealed class SunnyHttpClient(HttpClient http)
{
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

    public async Task<HttpResponseMessage> PutWeightAsync(int highlightId, SetWeightRequest request, CancellationToken ct = default)
    {
        var response = await http.PutAsJsonAsync($"/highlights/{highlightId}/weight", request, SunnyJsonContext.Default.SetWeightRequest, ct).ConfigureAwait(false);
        return response;
    }

    public async Task<List<WeightedHighlightDto>> GetWeightsAsync(CancellationToken ct = default)
        => (await http.GetFromJsonAsync("/highlights/weights", SunnyJsonContext.Default.ListWeightedHighlightDto, ct).ConfigureAwait(false))!;
}
