using Microsoft.Extensions.DependencyInjection;
using RichardSzalay.MockHttp;
using Spectre.Console.Cli;
using SunnySunday.Cli.Commands;
using SunnySunday.Cli.Infrastructure;

namespace SunnySunday.Tests.Cli;

public sealed class SyncCommandTests : IDisposable
{
    private readonly MockHttpMessageHandler _mockHttp = new();
    private readonly string _tempDir;

    public SyncCommandTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"sunny-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        _mockHttp.Dispose();
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    [Fact]
    public async Task Sync_WithValidFile_ReturnsZero()
    {
        var filePath = CreateClippingsFile(SampleClippings);

        _mockHttp.When(HttpMethod.Post, "http://localhost:5000/sync")
            .Respond("application/json", """
                {"newHighlights":5,"duplicateHighlights":2,"newBooks":3,"newAuthors":2}
                """);

        var exitCode = await RunSyncCommand(filePath);

        Assert.Equal(0, exitCode);
    }

    [Fact]
    public async Task Sync_WithEmptyFile_ReturnsZero()
    {
        var filePath = CreateClippingsFile("");

        var exitCode = await RunSyncCommand(filePath);

        Assert.Equal(0, exitCode);
    }

    [Fact]
    public async Task Sync_FileNotFound_ReturnsOne()
    {
        var exitCode = await RunSyncCommand("/nonexistent/path/My Clippings.txt");

        Assert.Equal(1, exitCode);
    }

    [Fact]
    public async Task Sync_ServerUnreachable_ReturnsOne()
    {
        var filePath = CreateClippingsFile(SampleClippings);

        _mockHttp.When(HttpMethod.Post, "http://localhost:5000/sync")
            .Throw(new HttpRequestException("Connection refused"));

        var exitCode = await RunSyncCommand(filePath);

        Assert.Equal(1, exitCode);
    }

    private async Task<int> RunSyncCommand(string filePath)
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
            config.AddCommand<SyncCommand>("sync");
        });

        return await app.RunAsync(["sync", filePath]);
    }

    private string CreateClippingsFile(string content)
    {
        var filePath = Path.Combine(_tempDir, "My Clippings.txt");
        File.WriteAllText(filePath, content);
        return filePath;
    }

    private const string SampleClippings = """
        The Pragmatic Programmer (David Thomas;Andrew Hunt)
        - Your Highlight on Location 150-152 | Added on Monday, January 15, 2024 12:30:00 PM

        Care About Your Craft
        ==========
        The Pragmatic Programmer (David Thomas;Andrew Hunt)
        - Your Highlight on Location 200-205 | Added on Monday, January 15, 2024 1:00:00 PM

        Think! About Your Work
        ==========
        Clean Code (Robert C. Martin)
        - Your Highlight on Location 50-55 | Added on Tuesday, January 16, 2024 9:00:00 AM

        Clean code is simple and direct.
        ==========
        Clean Code (Robert C. Martin)
        - Your Highlight on Location 100-110 | Added on Tuesday, January 16, 2024 9:30:00 AM

        The ratio of time spent reading versus writing is well over 10 to 1.
        ==========
        Clean Code (Robert C. Martin)
        - Your Highlight on Location 150-160 | Added on Tuesday, January 16, 2024 10:00:00 AM

        Leave the campground cleaner than you found it.
        ==========
        """;
}
