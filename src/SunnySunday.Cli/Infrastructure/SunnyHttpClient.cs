using System.Net.Http.Json;
using SunnySunday.Core.Contracts;

namespace SunnySunday.Cli.Infrastructure;

/// <summary>
/// Typed HTTP client for all Sunny Sunday server API calls.
/// </summary>
public sealed class SunnyHttpClient(HttpClient http)
{
    public async Task<SyncResponse> PostSyncAsync(SyncRequest request, CancellationToken ct = default)
    {
        var response = await http.PostAsJsonAsync("/sync", request, ct).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<SyncResponse>(ct).ConfigureAwait(false))!;
    }

    public async Task<SettingsResponse> GetSettingsAsync(CancellationToken ct = default)
        => (await http.GetFromJsonAsync<SettingsResponse>("/settings", ct).ConfigureAwait(false))!;

    public async Task<SettingsResponse> PutSettingsAsync(UpdateSettingsRequest request, CancellationToken ct = default)
    {
        var response = await http.PutAsJsonAsync("/settings", request, ct).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<SettingsResponse>(ct).ConfigureAwait(false))!;
    }

    public async Task<StatusResponse> GetStatusAsync(CancellationToken ct = default)
        => (await http.GetFromJsonAsync<StatusResponse>("/status", ct).ConfigureAwait(false))!;

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
        => (await http.GetFromJsonAsync<ExclusionsResponse>("/exclusions", ct).ConfigureAwait(false))!;

    public async Task<HttpResponseMessage> PutWeightAsync(int highlightId, SetWeightRequest request, CancellationToken ct = default)
    {
        var response = await http.PutAsJsonAsync($"/highlights/{highlightId}/weight", request, ct).ConfigureAwait(false);
        return response;
    }

    public async Task<List<WeightedHighlightDto>> GetWeightsAsync(CancellationToken ct = default)
        => (await http.GetFromJsonAsync<List<WeightedHighlightDto>>("/highlights/weights", ct).ConfigureAwait(false))!;
}
