using Microsoft.Extensions.DependencyInjection;
using Spectre.Console;
using Spectre.Console.Cli;
using SunnySunday.Cli.Commands;
using SunnySunday.Cli.Infrastructure;

var serverUrl = Environment.GetEnvironmentVariable("SUNNY_SERVER");

if (string.IsNullOrWhiteSpace(serverUrl))
{
    AnsiConsole.MarkupLine("[red]Error:[/] SUNNY_SERVER environment variable is not set.");
    AnsiConsole.MarkupLine("[grey]Set it to the server URL, e.g.:[/]");
    AnsiConsole.MarkupLine("[grey]  export SUNNY_SERVER=http://192.168.1.10:8080[/]");
    return 1;
}

if (!Uri.TryCreate(serverUrl, UriKind.Absolute, out var serverUri)
    || (serverUri.Scheme != "http" && serverUri.Scheme != "https"))
{
    AnsiConsole.MarkupLine($"[red]Error:[/] SUNNY_SERVER value is not a valid HTTP URL: [yellow]{serverUrl}[/]");
    return 1;
}

var services = new ServiceCollection();

services.AddHttpClient<SunnyHttpClient>(client =>
{
    client.BaseAddress = serverUri;
    client.Timeout = TimeSpan.FromSeconds(30);
}).AddSunnyResilience();

var registrar = new TypeRegistrar(services);

var app = new CommandApp(registrar);

app.Configure(config =>
{
    config.SetApplicationName("sunny");

    config.AddCommand<SyncCommand>("sync")
        .WithDescription("Parse and sync highlights from My Clippings.txt to the server.");
});

return await app.RunAsync(args);
