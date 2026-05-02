using Microsoft.Extensions.DependencyInjection;
using RichardSzalay.MockHttp;
using Spectre.Console.Cli;
using SunnySunday.Cli.Commands.Config;
using SunnySunday.Cli.Infrastructure;

namespace SunnySunday.Tests.Cli;

public sealed class ConfigCommandTests : IDisposable
{
    private readonly MockHttpMessageHandler _mockHttp = new();

    public void Dispose() => _mockHttp.Dispose();

    [Fact]
    public async Task ConfigSchedule_DailyWithValidTime_SendsPutWithTimezone()
    {
        var handler = _mockHttp.When(HttpMethod.Put, "http://localhost:5000/settings")
            .Respond("application/json", """
                {"schedule":"daily","deliveryDay":null,"deliveryTime":"08:00","count":5,"kindleEmail":"test@kindle.com","timezone":"Europe/Rome"}
                """);

        var exitCode = await RunConfigScheduleCommand("daily", "08:00");

        Assert.Equal(0, exitCode);
        Assert.Equal(1, _mockHttp.GetMatchCount(handler));
    }

    [Fact]
    public async Task ConfigSchedule_WeeklyWithValidTime_SendsPutWithTimezone()
    {
        var handler = _mockHttp.When(HttpMethod.Put, "http://localhost:5000/settings")
            .Respond("application/json", """
                {"schedule":"weekly","deliveryDay":"monday","deliveryTime":"09:00","count":5,"kindleEmail":"test@kindle.com","timezone":"Europe/Rome"}
                """);

        var exitCode = await RunConfigScheduleCommand("weekly", "09:00");

        Assert.Equal(0, exitCode);
        Assert.Equal(1, _mockHttp.GetMatchCount(handler));
    }

    [Fact]
    public async Task ConfigSchedule_InvalidTime_ReturnsOneWithoutHttpCall()
    {
        var handler = _mockHttp.When(HttpMethod.Put, "http://localhost:5000/settings")
            .Respond("application/json", "{}");

        var exitCode = await RunConfigScheduleCommand("daily", "25:00");

        Assert.Equal(1, exitCode);
        Assert.Equal(0, _mockHttp.GetMatchCount(handler));
    }

    [Fact]
    public async Task ConfigSchedule_InvalidCadence_ReturnsOneWithoutHttpCall()
    {
        var handler = _mockHttp.When(HttpMethod.Put, "http://localhost:5000/settings")
            .Respond("application/json", "{}");

        var exitCode = await RunConfigScheduleCommand("monthly", "08:00");

        Assert.Equal(1, exitCode);
        Assert.Equal(0, _mockHttp.GetMatchCount(handler));
    }

    [Fact]
    public async Task ConfigSchedule_Show_FetchesAndDisplaysCurrentSchedule()
    {
        var handler = _mockHttp.When(HttpMethod.Get, "http://localhost:5000/settings")
            .Respond("application/json", """
                {"schedule":"daily","deliveryDay":null,"deliveryTime":"08:00","count":5,"kindleEmail":"test@kindle.com","timezone":"Europe/Rome"}
                """);

        var exitCode = await RunConfigScheduleCommand("show");

        Assert.Equal(0, exitCode);
        Assert.Equal(1, _mockHttp.GetMatchCount(handler));
    }

    [Fact]
    public async Task ConfigSchedule_ServerUnreachable_ReturnsOne()
    {
        _mockHttp.When(HttpMethod.Put, "http://localhost:5000/settings")
            .Throw(new HttpRequestException("Connection refused"));

        var exitCode = await RunConfigScheduleCommand("daily", "08:00");

        Assert.Equal(1, exitCode);
    }

    private async Task<int> RunConfigScheduleCommand(params string[] args)
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
            config.AddBranch("config", cfg =>
            {
                cfg.AddCommand<ConfigScheduleCommand>("schedule");
            });
        });

        var fullArgs = new[] { "config", "schedule" }.Concat(args).ToArray();
        return await app.RunAsync(fullArgs);
    }
}
