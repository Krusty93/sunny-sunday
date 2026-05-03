using System.Net;
using Microsoft.Extensions.DependencyInjection;
using RichardSzalay.MockHttp;
using Spectre.Console.Cli;
using SunnySunday.Cli.Commands.Weight;
using SunnySunday.Cli.Infrastructure;

namespace SunnySunday.Tests.Cli;

public sealed class WeightCommandTests : IDisposable
{
    private readonly MockHttpMessageHandler _mockHttp = new();

    public void Dispose() => _mockHttp.Dispose();

    [Fact]
    public async Task WeightSet_ValidWeight_SendsPut()
    {
        var handler = _mockHttp.When(HttpMethod.Put, "http://localhost:5000/highlights/5/weight")
            .Respond(HttpStatusCode.OK);

        var exitCode = await RunWeightCommand("set", "5", "3");

        Assert.Equal(0, exitCode);
        Assert.Equal(1, _mockHttp.GetMatchCount(handler));
    }

    [Fact]
    public async Task WeightSet_WeightZero_ReturnsOneWithoutHttpCall()
    {
        var handler = _mockHttp.When(HttpMethod.Put, "*")
            .Respond(HttpStatusCode.OK);

        var exitCode = await RunWeightCommand("set", "5", "0");

        Assert.Equal(1, exitCode);
        Assert.Equal(0, _mockHttp.GetMatchCount(handler));
    }

    [Fact]
    public async Task WeightSet_WeightSix_ReturnsOneWithoutHttpCall()
    {
        var handler = _mockHttp.When(HttpMethod.Put, "*")
            .Respond(HttpStatusCode.OK);

        var exitCode = await RunWeightCommand("set", "5", "6");

        Assert.Equal(1, exitCode);
        Assert.Equal(0, _mockHttp.GetMatchCount(handler));
    }

    [Fact]
    public async Task WeightSet_NotFound_ReturnsOne()
    {
        _mockHttp.When(HttpMethod.Put, "http://localhost:5000/highlights/999/weight")
            .Respond(HttpStatusCode.NotFound);

        var exitCode = await RunWeightCommand("set", "999", "3");

        Assert.Equal(1, exitCode);
    }

    [Fact]
    public async Task WeightSet_ServerUnreachable_ReturnsOne()
    {
        _mockHttp.When(HttpMethod.Put, "http://localhost:5000/highlights/5/weight")
            .Throw(new HttpRequestException("Connection refused"));

        var exitCode = await RunWeightCommand("set", "5", "3");

        Assert.Equal(1, exitCode);
    }

    [Fact]
    public async Task WeightList_DisplaysTable()
    {
        var handler = _mockHttp.When(HttpMethod.Get, "http://localhost:5000/highlights/weights")
            .Respond("application/json", """
                [
                    {"id":1,"text":"Some highlight","bookTitle":"Clean Code","weight":3},
                    {"id":2,"text":"Another one","bookTitle":"The Pragmatic Programmer","weight":5}
                ]
                """);

        var exitCode = await RunWeightCommand("list");

        Assert.Equal(0, exitCode);
        Assert.Equal(1, _mockHttp.GetMatchCount(handler));
    }

    [Fact]
    public async Task WeightList_Empty_DisplaysNoWeightsMessage()
    {
        _mockHttp.When(HttpMethod.Get, "http://localhost:5000/highlights/weights")
            .Respond("application/json", "[]");

        var exitCode = await RunWeightCommand("list");

        Assert.Equal(0, exitCode);
    }

    [Fact]
    public async Task WeightList_ServerUnreachable_ReturnsOne()
    {
        _mockHttp.When(HttpMethod.Get, "http://localhost:5000/highlights/weights")
            .Throw(new HttpRequestException("Connection refused"));

        var exitCode = await RunWeightCommand("list");

        Assert.Equal(1, exitCode);
    }

    private async Task<int> RunWeightCommand(params string[] args)
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
            config.AddBranch("weight", wgt =>
            {
                wgt.AddCommand<WeightSetCommand>("set");
                wgt.AddCommand<WeightListCommand>("list");
            });
        });

        var fullArgs = new[] { "weight" }.Concat(args).ToArray();
        return await app.RunAsync(fullArgs);
    }
}
