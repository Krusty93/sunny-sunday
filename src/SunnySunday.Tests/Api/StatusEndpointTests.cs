using System.Net;
using System.Net.Http.Json;
using SunnySunday.Core.Contracts;

namespace SunnySunday.Tests.Api;

public sealed class StatusEndpointTests : IDisposable
{
    private readonly SunnyTestApplicationFactory _factory;
    private readonly HttpClient _client;

    public StatusEndpointTests()
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
    public async Task GetStatus_AfterSeedingData_ReturnsCorrectCounts()
    {
        var syncRequest = BuildRequest(bookCount: 10, highlightsPerBook: 10, authorCount: 5);
        var syncResponse = await _client.PostAsJsonAsync("/sync", syncRequest);
        Assert.Equal(HttpStatusCode.OK, syncResponse.StatusCode);

        var response = await _client.GetAsync("/status");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<StatusResponse>();
        Assert.NotNull(result);
        Assert.Equal(100, result.TotalHighlights);
        Assert.Equal(10, result.TotalBooks);
        Assert.Equal(5, result.TotalAuthors);
        Assert.Equal(0, result.ExcludedHighlights);
        Assert.Null(result.NextRecap);
    }

    [Fact]
    public async Task GetStatus_EmptyDatabase_ReturnsAllZeros()
    {
        var response = await _client.GetAsync("/status");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<StatusResponse>();
        Assert.NotNull(result);
        Assert.Equal(0, result.TotalHighlights);
        Assert.Equal(0, result.TotalBooks);
        Assert.Equal(0, result.TotalAuthors);
        Assert.Equal(0, result.ExcludedHighlights);
        Assert.Equal(0, result.ExcludedBooks);
        Assert.Equal(0, result.ExcludedAuthors);
        Assert.Null(result.NextRecap);
    }

    private static SyncRequest BuildRequest(int bookCount, int highlightsPerBook, int authorCount) =>
        new()
        {
            Books = Enumerable.Range(1, bookCount).Select(b => new SyncBookRequest
            {
                Title = $"Book {b}",
                Author = $"Author {((b - 1) % authorCount) + 1}",
                Highlights = Enumerable.Range(1, highlightsPerBook)
                    .Select(h => new SyncHighlightRequest { Text = $"Book {b} highlight {h}" })
                    .ToList()
            }).ToList()
        };
}
