using System.Net;
using System.Net.Http.Json;
using SunnySunday.Core.Contracts;

namespace SunnySunday.Tests.Api;

public sealed class SettingsEndpointTests : IDisposable
{
    private readonly SunnyTestApplicationFactory _factory;
    private readonly HttpClient _client;

    public SettingsEndpointTests()
    {
        _factory = new SunnyTestApplicationFactory();
        _client = _factory.CreateClient();
    }

    public void Dispose()
    {
        _client.Dispose();
        _factory.Dispose();
    }

    [Fact]
    public async Task GetSettings_WithNoStoredSettings_ReturnsDefaults()
    {
        var response = await _client.GetAsync("/settings");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<SettingsResponse>();
        Assert.NotNull(result);
        Assert.Equal("daily", result.Schedule);
        Assert.Null(result.DeliveryDay);
        Assert.Equal("18:00", result.DeliveryTime);
        Assert.Equal(3, result.Count);
        Assert.Equal(string.Empty, result.KindleEmail);
    }

    [Fact]
    public async Task PutSettings_UpdatesSchedule_AndPreservesOtherFields()
    {
        var response = await _client.PutAsJsonAsync("/settings", new UpdateSettingsRequest { Schedule = "weekly" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var getResponse = await _client.GetAsync("/settings");
        var result = await getResponse.Content.ReadFromJsonAsync<SettingsResponse>();

        Assert.NotNull(result);
        Assert.Equal("weekly", result.Schedule);
        Assert.Null(result.DeliveryDay);
        Assert.Equal("18:00", result.DeliveryTime);
        Assert.Equal(3, result.Count);
        Assert.Equal(string.Empty, result.KindleEmail);
    }

    [Fact]
    public async Task PutSettings_CountZero_Returns422WithFieldError()
    {
        var response = await _client.PutAsJsonAsync("/settings", new UpdateSettingsRequest { Count = 0 });

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("count", body);
    }

    [Fact]
    public async Task PutSettings_CountSixteen_Returns422WithFieldError()
    {
        var response = await _client.PutAsJsonAsync("/settings", new UpdateSettingsRequest { Count = 16 });

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("count", body);
    }

    [Fact]
    public async Task PutSettings_ValidKindleEmail_IsPersisted()
    {
        var putResponse = await _client.PutAsJsonAsync("/settings", new UpdateSettingsRequest { KindleEmail = "user+recap@kindle.com" });

        Assert.Equal(HttpStatusCode.OK, putResponse.StatusCode);

        var getResponse = await _client.GetAsync("/settings");
        var result = await getResponse.Content.ReadFromJsonAsync<SettingsResponse>();

        Assert.NotNull(result);
        Assert.Equal("user+recap@kindle.com", result.KindleEmail);
    }

    [Fact]
    public async Task PutSettings_InvalidKindleEmail_Returns422WithFieldError()
    {
        var response = await _client.PutAsJsonAsync("/settings", new UpdateSettingsRequest { KindleEmail = "invalid" });

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("kindleEmail", body);
    }
}
