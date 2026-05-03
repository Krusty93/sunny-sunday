using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Serilog;
using Serilog.Core;
using Spectre.Console;
using Spectre.Console.Cli;
using SunnySunday.Cli.Commands;
using SunnySunday.Cli.Commands.Config;
using SunnySunday.Cli.Commands.Exclude;
using SunnySunday.Cli.Commands.Weight;
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

var levelSwitch = new LoggingLevelSwitch();

Log.Logger = new LoggerConfiguration()
    .MinimumLevel.ControlledBy(levelSwitch)
    .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}")
    .CreateLogger();

services.AddLogging(builder => builder.AddSerilog(dispose: true));

services.AddHttpClient<SunnyHttpClient>(client =>
{
    client.BaseAddress = serverUri;
    client.Timeout = TimeSpan.FromSeconds(30);
}).AddSunnyResilience();

var registrar = new TypeRegistrar(services);

var app = new CommandApp(registrar);

app.Configure(config =>
{
    var assembly = typeof(SyncCommand).Assembly;
    var applicationName = assembly.GetName().Name ?? "sunny sunday";
    var version = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
        ?? assembly.GetName().Version?.ToString()
        ?? "unknown";

    config.SetApplicationName(applicationName);
    config.SetApplicationVersion(version);
    config.SetInterceptor(new LogInterceptor(levelSwitch));

    config.AddCommand<SyncCommand>("sync")
        .WithDescription("Parse and sync highlights from My Clippings.txt to the server.");

    config.AddBranch("config", cfg =>
    {
        cfg.SetDescription("Manage server settings.");
        cfg.AddCommand<ConfigScheduleCommand>("schedule")
            .WithDescription("Configure recap schedule (cadence and time).");
        cfg.AddCommand<ConfigCountCommand>("count")
            .WithDescription("Configure number of highlights per recap.");
        cfg.AddCommand<ConfigShowCommand>("show")
            .WithDescription("Display all current settings.");
    });

    config.AddBranch("exclude", exc =>
    {
        exc.SetDescription("Manage exclusions from recaps.");
        exc.AddCommand<ExcludeAddCommand>("add")
            .WithDescription("Exclude a highlight, book, or author from recaps.");
        exc.AddCommand<ExcludeRemoveCommand>("remove")
            .WithDescription("Remove an exclusion.");
        exc.AddCommand<ExcludeListCommand>("list")
            .WithDescription("List all current exclusions.");
    });

    config.AddBranch("weight", wgt =>
    {
        wgt.SetDescription("Manage highlight recap weights.");
        wgt.AddCommand<WeightSetCommand>("set")
            .WithDescription("Set the recap weight for a highlight (1–5).");
        wgt.AddCommand<WeightListCommand>("list")
            .WithDescription("List all highlights with custom weights.");
    });
});

return await app.RunAsync(args);
