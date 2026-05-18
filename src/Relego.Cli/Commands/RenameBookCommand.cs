using System.ComponentModel;
using System.Net;
using Microsoft.Extensions.Logging;
using Spectre.Console;
using Spectre.Console.Cli;
using Relego.Cli.Infrastructure;
using Relego.Core.Contracts;

namespace Relego.Cli.Commands;

/// <summary>
/// Renames a book stored in the server.
/// Usage: relego rename-book &lt;id&gt; &lt;new-title&gt;
/// </summary>
public sealed class RenameBookCommand(RelegoHttpClient client, ILogger<RenameBookCommand> logger)
    : ServerCommand<RenameBookCommand.Settings>
{
    protected override ILogger Logger => logger;

    public sealed class Settings : CommandSettings
    {
        [CommandArgument(0, "<id>")]
        [Description("ID of the book to rename.")]
        public int Id { get; set; }

        [CommandArgument(1, "<title>")]
        [Description("New title for the book.")]
        public string Title { get; set; } = string.Empty;
    }

    protected override async Task<int> ExecuteAsync(CommandContext context, Settings settings, CancellationToken cancellation)
    {
        var newTitle = settings.Title.Trim();

        if (string.IsNullOrWhiteSpace(newTitle))
        {
            AnsiConsole.MarkupLine("[red]Error:[/] Title must not be empty.");
            return 1;
        }

        logger.LogDebug("Renaming book {Id} to {Title}", settings.Id, newTitle);

        HttpResponseMessage response;
        try
        {
            response = await client.RenameBookAsync(settings.Id, new RenameBookRequest { Title = newTitle }, cancellation);
        }
        catch (HttpRequestException ex)
        {
            return HandleServerError(ex);
        }

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            AnsiConsole.MarkupLine($"[red]Error:[/] Book with ID [yellow]{settings.Id}[/] not found.");
            return 1;
        }

        if (response.StatusCode == HttpStatusCode.Conflict)
        {
            AnsiConsole.MarkupLine($"[red]Error:[/] A book titled [yellow]{Markup.Escape(newTitle)}[/] by the same author already exists.");
            return 1;
        }

        response.EnsureSuccessStatusCode();
        AnsiConsole.MarkupLine($"[green]✓[/] Book [bold]{settings.Id}[/] renamed to [bold]{Markup.Escape(newTitle)}[/].");
        return 0;
    }
}
