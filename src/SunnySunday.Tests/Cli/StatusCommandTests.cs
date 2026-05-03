using Microsoft.Extensions.DependencyInjection;
using RichardSzalay.MockHttp;
using Spectre.Console.Cli;
using SunnySunday.Cli.Commands;
using SunnySunday.Cli.Infrastructure;

namespace SunnySunday.Tests.Cli;

public sealed class StatusCommandTests : IDisposable
{
    private readonly MockHttpMessageHandler _mockHttp = new();

    public void Dispose() => _mockHttp.Dispose();

    [Fact]
    public async Task Status_NormalResponse_ReturnsZero()
    {
        var handler = _mockHttp.When(HttpMethod.Get, "http://localhost:5000/status")
            .Respond("application/json", """
                {
                    "totalHighlights": 120,
                    "totalBooks": 15,
                    "totalAuthors": 10,
                    "excludedHighlights": 2,
                    "excludedBooks": 1,
                    "excludedAuthors": 0,
                    "nextRecap": "2026-05-04T08:00:00Z",
                    "lastRecapStatus": "delivered",
                    "lastRecapError": null
                }
                """);

        var exitCode = await RunStatusCommand();

        Assert.Equal(0, exitCode);
        Assert.Equal(1, _mockHttp.GetMatchCount(handler));
    }

    [Fact]
    public async Task Status_ServerUnreachable_ReturnsOne()
    {
        _mockHttp.When(HttpMethod.Get, "http://localhost:5000/status")
            .Throw(new HttpRequestException("Connection refused"));

        var exitCode = await RunStatusCommand();

        Assert.Equal(1, exitCode);
    }

    [Fact]
    public async Task Status_UtcTimestampConvertedToLocal()
    {
        // Arrange: use a fixed UTC time — the command must parse it without throwing
        _mockHttp.When(HttpMethod.Get, "http://localhost:5000/status")
            .Respond("application/json", """
                {
                    "totalHighlights": 10,
                    "totalBooks": 2,
                    "totalAuthors": 1,
                    "excludedHighlights": 0,
                    "excludedBooks": 0,
                    "excludedAuthors": 0,
                    "nextRecap": "2026-05-04T08:00:00Z",
                    "lastRecapStatus": null,
                    "lastRecapError": null
                }
                """);

        // Act: command must succeed (conversion does not throw)
        var exitCode = await RunStatusCommand();

        Assert.Equal(0, exitCode);
    }

    [Fact]
    public async Task Status_FailedLastRecap_ReturnsZero()
    {
        _mockHttp.When(HttpMethod.Get, "http://localhost:5000/status")
            .Respond("application/json", """
                {
                    "totalHighlights": 5,
                    "totalBooks": 1,
                    "totalAuthors": 1,
                    "excludedHighlights": 0,
                    "excludedBooks": 0,
                    "excludedAuthors": 0,
                    "nextRecap": null,
                    "lastRecapStatus": "failed",
                    "lastRecapError": "SMTP connection timeout"
                }
                """);

        var exitCode = await RunStatusCommand();

        Assert.Equal(0, exitCode);
    }

    [Fact]
    public async Task Status_NoNextRecap_ReturnsZero()
    {
        _mockHttp.When(HttpMethod.Get, "http://localhost:5000/status")
            .Respond("application/json", """
                {
                    "totalHighlights": 0,
                    "totalBooks": 0,
                    "totalAuthors": 0,
                    "excludedHighlights": 0,
                    "excludedBooks": 0,
                    "excludedAuthors": 0,
                    "nextRecap": null,
                    "lastRecapStatus": null,
                    "lastRecapError": null
                }
                """);

        var exitCode = await RunStatusCommand();

        Assert.Equal(0, exitCode);
    }

    private async Task<int> RunStatusCommand()
    {
        var services = new ServiceCollection();
        services.AddLogging();

        services.AddTransient(_ =>
        {
            var httpClient = _mockHttp.ToHttpClient();
            httpClient.BaseAddress = new Uri("http://localhost:5000");
            return new SunnyHttpClient(httpClient);
        });

        var registrar = new TypeRegistrar(services);
        var app = new CommandApp(registrar);

        app.Configure(config =>
        {
            config.SetApplicationName("sunny");
            config.AddCommand<StatusCommand>("status");
        });

        return await app.RunAsync(["status"]);
    }
}
