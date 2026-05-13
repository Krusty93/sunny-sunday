using System.Reflection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;
using Spectre.Console;
using Spectre.Console.Cli;
using SunnySunday.Cli.Commands;
using SunnySunday.Cli.Commands.Config;
using SunnySunday.Cli.Commands.Exclude;
using SunnySunday.Cli.Commands.Weight;
using SunnySunday.Cli.Infrastructure;
using SunnySunday.Cli.Sync;
using SunnySunday.Cli.Tui;

var builder = Host.CreateApplicationBuilder(args);
Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .CreateLogger();

builder.Services.AddLogging(loggingBuilder =>
{
    loggingBuilder.AddSerilog(Log.Logger, dispose: true);
});

string? serverUrl = builder.Configuration["sunny_server"];
var validationResult = ServerUrlValidator.Validate(serverUrl, out Uri? serverUri);
if (validationResult == ServerUrlValidator.ValidationResult.Malformed)
{
    AnsiConsole.MarkupLine($"[red]Error:[/] SUNNY_SERVER value is not a valid HTTP URL: [yellow]{serverUrl}[/]");
    return 1;
}

var normalizedServerUrl = serverUri!.ToString().TrimEnd('/');

Assembly assembly = typeof(SyncCommand).Assembly;
string applicationName = assembly.GetName().Name ?? "sunny sunday";
string version = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
    ?? assembly.GetName().Version?.ToString()
    ?? "unknown";

builder.Services.AddHttpClient<SunnyHttpClient>((sp, client) =>
{
    var config = sp.GetRequiredService<IConfiguration>();
    var configuredServerUrl = config["sunny_server"]!;
    client.BaseAddress = new Uri(configuredServerUrl);
    client.Timeout = TimeSpan.FromSeconds(30);
}).AddSunnyResilience();
builder.Services.AddTransient<ClippingsSyncWorkflow>();

using IHost host = builder.Build();

if (TuiModeDetector.Detect(args, Console.IsInputRedirected) == StartupMode.Tui)
{
    var client = host.Services.GetRequiredService<SunnyHttpClient>();
    var syncWorkflow = host.Services.GetRequiredService<ClippingsSyncWorkflow>();
    var tuiApp = new TuiApp(client, syncWorkflow, normalizedServerUrl, version);
    await tuiApp.RunAsync(CancellationToken.None);
    return 0;
}

var registrar = new TypeRegistrar(host.Services);

var app = new CommandApp(registrar);

app.Configure(config =>
{
    config.SetApplicationName(applicationName);
    config.SetApplicationVersion(version);

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
