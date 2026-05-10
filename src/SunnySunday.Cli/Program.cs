using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Core;
using Serilog.Settings.Configuration;
using Spectre.Console;
using Spectre.Console.Cli;
using SunnySunday.Cli.Commands;
using SunnySunday.Cli.Commands.Config;
using SunnySunday.Cli.Commands.Exclude;
using SunnySunday.Cli.Commands.Weight;
using SunnySunday.Cli.Infrastructure;
using SunnySunday.Cli.Tui;

const string DefaultServerUrl = "http://localhost:8080";

var serverUrl = Environment.GetEnvironmentVariable("SUNNY_SERVER");

var validationResult = ServerUrlValidator.Validate(serverUrl, out var serverUri);

if (validationResult == ServerUrlValidator.ValidationResult.Missing)
{
    serverUrl = DefaultServerUrl;
    serverUri = new Uri(DefaultServerUrl);
    AnsiConsole.MarkupLine($"[grey]SUNNY_SERVER not set — using default: {DefaultServerUrl}[/]");
}
else if (validationResult == ServerUrlValidator.ValidationResult.Malformed)
{
    AnsiConsole.MarkupLine($"[red]Error:[/] SUNNY_SERVER value is not a valid HTTP URL: [yellow]{serverUrl}[/]");
    return 1;
}

LoggingLevelSwitch? levelSwitch = null;
var assembly = typeof(SyncCommand).Assembly;
var applicationName = assembly.GetName().Name ?? "sunny sunday";
var version = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
    ?? assembly.GetName().Version?.ToString()
    ?? "unknown";
var normalizedServerUrl = serverUri!.ToString().TrimEnd('/');

var hostBuilder = Host.CreateDefaultBuilder(args)
    .ConfigureLogging(logging => logging.ClearProviders())
    .UseSerilog((context, _, loggerConfiguration) =>
    {
        loggerConfiguration.ReadFrom.Configuration(
            context.Configuration,
            new ConfigurationReaderOptions
            {
                OnLevelSwitchCreated = (switchName, loggingSwitch) =>
                {
                    if (switchName.Equals("$cli", StringComparison.Ordinal))
                    {
                        levelSwitch = loggingSwitch;
                    }
                }
            });
    })
    .ConfigureServices((_, services) =>
    {
        services.AddHttpClient<SunnyHttpClient>(client =>
        {
            client.BaseAddress = serverUri;
            client.Timeout = TimeSpan.FromSeconds(30);
        }).AddSunnyResilience();
    });

if (TuiModeDetector.Detect(args, Console.IsInputRedirected) == StartupMode.Tui)
{
    using var tuiHost = hostBuilder.Build();
    var client = tuiHost.Services.GetRequiredService<SunnyHttpClient>();
    var tuiApp = new TuiApp(client, normalizedServerUrl, version);
    await tuiApp.RunAsync(CancellationToken.None);
    return 0;
}

using var cliHost = hostBuilder.Build();

var registrar = new TypeRegistrar(cliHost.Services);

var app = new CommandApp(registrar);

app.Configure(config =>
{
    config.SetApplicationName(applicationName);
    config.SetApplicationVersion(version);
    config.SetInterceptor(new LogInterceptor(() => levelSwitch
        ?? throw new InvalidOperationException("CLI log level switch '$cli' was not initialized from configuration.")));

    config.AddCommand<SyncCommand>("sync")
        .WithDescription("Parse and sync highlights from My Clippings.txt to the server.");

    config.AddCommand<StatusCommand>("status")
        .WithDescription("Display server health and aggregate state.");

    config.AddBranch("config", cfg =>
    {
        cfg.SetDescription("Manage server settings.");
        cfg.AddCommand<ConfigScheduleCommand>("schedule")
            .WithDescription("Configure recap schedule (cadence and time).");
        cfg.AddCommand<ConfigCountCommand>("count")
            .WithDescription("Configure number of highlights per recap.");
        cfg.AddCommand<ConfigKindleEmailCommand>("kindle-email")
            .WithDescription("Set the Kindle delivery email address.");
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
