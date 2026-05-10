using Spectre.Console;
using Spectre.Console.Rendering;
using SunnySunday.Cli.Infrastructure;
using SunnySunday.Cli.Tui.ViewModels;
using SunnySunday.Core.Contracts;

namespace SunnySunday.Cli.Tui;

public sealed class HighlightDetailScreen : IScreen
{
    private readonly SunnyHttpClient _client;
    private readonly List<HighlightViewModel> _highlights;
    private readonly string _title;
    private readonly string _author;
    private readonly int _bookId;
    private readonly int _authorId;
    private bool _isBookExcluded;
    private bool _isAuthorExcluded;
    private string? _statusMessage;

    public HighlightDetailScreen(BookViewModel book, SunnyHttpClient client)
    {
        ArgumentNullException.ThrowIfNull(book);
        ArgumentNullException.ThrowIfNull(client);

        Book = book;
        _client = client;
        _title = book.Title;
        _author = book.Author;
        _bookId = book.BookId;
        _authorId = book.AuthorId;
        _isBookExcluded = book.IsBookExcluded;
        _isAuthorExcluded = book.IsAuthorExcluded;
        _highlights = [.. book.Highlights];
    }

    public BookViewModel Book { get; }

    public int SelectedIndex { get; private set; }

    public bool ActionMenuOpen { get; private set; }

    public int ActionMenuIndex { get; private set; }

    public bool DeleteConfirmationOpen { get; private set; }

    public string? StatusMessage => _statusMessage;

    public IReadOnlyList<HighlightViewModel> Highlights => _highlights;

    public string KeyHints => "[↑↓] Navigate · [Enter] Actions · [R] Refresh · [Esc] Back";

    public async Task InitializeAsync(CancellationToken cancellationToken)
    {
        await RefreshAsync(cancellationToken).ConfigureAwait(false);
    }

    public IRenderable Render()
    {
        var rows = new List<IRenderable>
        {
            new Panel(BuildBookSummary())
            {
                Header = new PanelHeader($"{Markup.Escape(_title)} [grey]by {Markup.Escape(_author)}[/]")
            },
            BuildHighlightsTable()
        };

        if (ActionMenuOpen)
        {
            rows.Add(BuildActionMenu());
        }

        if (DeleteConfirmationOpen)
        {
            rows.Add(new Panel(new Markup("[yellow]Delete this highlight? Press [bold]Y[/] to confirm or [bold]N[/]/[bold]Esc[/] to cancel.[/]"))
            {
                Header = new PanelHeader("Confirm Delete")
            });
        }

        if (!string.IsNullOrWhiteSpace(_statusMessage))
        {
            rows.Add(new Markup(Markup.Escape(_statusMessage)));
        }

        return new Rows(rows);
    }

    public async Task<ScreenResult> HandleKeyAsync(ConsoleKeyInfo key, CancellationToken cancellationToken)
    {
        if (DeleteConfirmationOpen)
        {
            return await HandleDeleteConfirmationAsync(key, cancellationToken).ConfigureAwait(false);
        }

        if (ActionMenuOpen)
        {
            return await HandleActionMenuAsync(key, cancellationToken).ConfigureAwait(false);
        }

        switch (key.Key)
        {
            case ConsoleKey.UpArrow:
                MoveSelection(-1);
                return ScreenResult.Stay();
            case ConsoleKey.DownArrow:
                MoveSelection(1);
                return ScreenResult.Stay();
            case ConsoleKey.Enter:
                if (_highlights.Count == 0)
                {
                    return ScreenResult.Stay();
                }

                ActionMenuOpen = true;
                ActionMenuIndex = 0;
                _statusMessage = null;
                return ScreenResult.Stay();
            case ConsoleKey.R:
                await RefreshAsync(cancellationToken).ConfigureAwait(false);
                return ScreenResult.Stay();
            case ConsoleKey.Escape:
                return ScreenResult.Pop();
            case ConsoleKey.C when key.Modifiers.HasFlag(ConsoleModifiers.Control):
                return ScreenResult.Quit();
            default:
                return ScreenResult.Stay();
        }
    }

    private IRenderable BuildBookSummary()
    {
        var highlightCount = _highlights.Count == 1 ? "1 highlight" : $"{_highlights.Count} highlights";
        var bookState = _isBookExcluded ? "excluded" : "included";
        var authorState = _isAuthorExcluded ? "excluded" : "included";

        return new Rows(
            new Markup($"[bold]{Markup.Escape(highlightCount)}[/]"),
            new Markup($"Book: [grey]{bookState}[/] · Author: [grey]{authorState}[/]"));
    }

    private IRenderable BuildHighlightsTable()
    {
        if (_highlights.Count == 0)
        {
            return new Panel(new Markup("[grey]This book has no highlights.[/]"))
            {
                Header = new PanelHeader("Highlights")
            };
        }

        var table = new Table().Border(TableBorder.Rounded).Expand();
        table.AddColumn("Highlight");
        table.AddColumn("Weight");
        table.AddColumn("State");

        for (var index = 0; index < _highlights.Count; index++)
        {
            var highlight = _highlights[index];
            var prefix = index == SelectedIndex ? "[bold yellow]>[/] " : string.Empty;
            var text = Markup.Escape(Truncate(highlight.Text, 90));
            var weight = (highlight.Weight ?? 3).ToString();
            var state = IsEffectivelyExcluded(highlight) ? "Excluded" : "Included";

            table.AddRow($"{prefix}{text}", weight, state);
        }

        return new Panel(table)
        {
            Header = new PanelHeader("Highlights")
        };
    }

    private IRenderable BuildActionMenu()
    {
        var menu = new Rows(GetActionLabels()
            .Select((label, index) =>
            {
                var prefix = index == ActionMenuIndex ? "[bold yellow]>[/] " : "  ";
                return (IRenderable)new Markup(prefix + Markup.Escape(label));
            }));

        return new Panel(menu)
        {
            Header = new PanelHeader("Actions")
        };
    }

    private async Task<ScreenResult> HandleActionMenuAsync(ConsoleKeyInfo key, CancellationToken cancellationToken)
    {
        switch (key.Key)
        {
            case ConsoleKey.UpArrow:
                ActionMenuIndex = Math.Max(0, ActionMenuIndex - 1);
                return ScreenResult.Stay();
            case ConsoleKey.DownArrow:
                ActionMenuIndex = Math.Min(GetActionLabels().Count - 1, ActionMenuIndex + 1);
                return ScreenResult.Stay();
            case ConsoleKey.Escape:
                ActionMenuOpen = false;
                _statusMessage = null;
                return ScreenResult.Stay();
            case ConsoleKey.Enter:
                return await ExecuteActionAsync(cancellationToken).ConfigureAwait(false);
            default:
                return ScreenResult.Stay();
        }
    }

    private async Task<ScreenResult> HandleDeleteConfirmationAsync(ConsoleKeyInfo key, CancellationToken cancellationToken)
    {
        if (key.Key == ConsoleKey.Escape || key.Key == ConsoleKey.N)
        {
            DeleteConfirmationOpen = false;
            return ScreenResult.Stay();
        }

        if (key.Key == ConsoleKey.Y)
        {
            await DeleteSelectedHighlightAsync(cancellationToken).ConfigureAwait(false);
        }

        return ScreenResult.Stay();
    }

    private async Task<ScreenResult> ExecuteActionAsync(CancellationToken cancellationToken)
    {
        switch (ActionMenuIndex)
        {
            case 0:
                await CycleWeightAsync(cancellationToken).ConfigureAwait(false);
                return ScreenResult.Stay();
            case 1:
                await ToggleHighlightExclusionAsync(cancellationToken).ConfigureAwait(false);
                return ScreenResult.Stay();
            case 2:
                await ToggleBookExclusionAsync(cancellationToken).ConfigureAwait(false);
                return ScreenResult.Stay();
            case 3:
                await ToggleAuthorExclusionAsync(cancellationToken).ConfigureAwait(false);
                return ScreenResult.Stay();
            case 4:
                DeleteConfirmationOpen = true;
                ActionMenuOpen = false;
                _statusMessage = null;
                return ScreenResult.Stay();
            default:
                return ScreenResult.Stay();
        }
    }

    private async Task RefreshAsync(CancellationToken cancellationToken)
    {
        var highlights = new List<HighlightItemDto>();
        var page = 1;
        const int pageSize = 200;

        while (true)
        {
            var response = await _client.GetHighlightsAsync(page, pageSize, query: null, cancellationToken).ConfigureAwait(false);
            highlights.AddRange(response.Items.Where(item => item.BookId == _bookId));

            if (response.Page * response.PageSize >= response.Total)
            {
                break;
            }

            page++;
        }

        var exclusions = await _client.GetExclusionsAsync(cancellationToken).ConfigureAwait(false);
        var weights = await _client.GetWeightsAsync(cancellationToken).ConfigureAwait(false);
        var excludedHighlightIds = exclusions.Highlights.Select(highlight => highlight.Id).ToHashSet();
        var weightedHighlights = weights.ToDictionary(weight => weight.Id, weight => weight.Weight);

        _highlights.Clear();
        _highlights.AddRange(highlights.Select(item => new HighlightViewModel(
            item.Id,
            item.BookId,
            item.AuthorId,
            item.Text,
            item.BookTitle,
            item.AuthorName,
            excludedHighlightIds.Contains(item.Id),
            weightedHighlights.TryGetValue(item.Id, out var weight) ? weight : null)));

        _isBookExcluded = exclusions.Books.Any(book => book.Id == _bookId);
        _isAuthorExcluded = exclusions.Authors.Any(author => author.Id == _authorId);
        SelectedIndex = Math.Clamp(SelectedIndex, 0, Math.Max(0, _highlights.Count - 1));
        ActionMenuIndex = Math.Clamp(ActionMenuIndex, 0, Math.Max(0, GetActionLabels().Count - 1));
        ActionMenuOpen = false;
        DeleteConfirmationOpen = false;
        _statusMessage = null;
    }

    private async Task CycleWeightAsync(CancellationToken cancellationToken)
    {
        var currentHighlight = GetSelectedHighlight();
        if (currentHighlight is null)
        {
            return;
        }

        var nextWeight = ((currentHighlight.Weight ?? 3) % 5) + 1;
        using var response = await _client.PutWeightAsync(currentHighlight.Id, new SetWeightRequest { Weight = nextWeight }, cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            _statusMessage = $"Weight update failed: {(int)response.StatusCode} {response.ReasonPhrase}";
            return;
        }

        _highlights[SelectedIndex] = currentHighlight with { Weight = nextWeight };
        ActionMenuOpen = false;
        _statusMessage = $"Weight updated to {nextWeight}.";
    }

    private async Task ToggleHighlightExclusionAsync(CancellationToken cancellationToken)
    {
        var currentHighlight = GetSelectedHighlight();
        if (currentHighlight is null)
        {
            return;
        }

        using var response = currentHighlight.IsExcluded
            ? await _client.DeleteExcludeAsync("highlight", currentHighlight.Id, cancellationToken).ConfigureAwait(false)
            : await _client.PostExcludeAsync("highlight", currentHighlight.Id, cancellationToken).ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
        {
            _statusMessage = $"Highlight update failed: {(int)response.StatusCode} {response.ReasonPhrase}";
            return;
        }

        _highlights[SelectedIndex] = currentHighlight with { IsExcluded = !currentHighlight.IsExcluded };
        ActionMenuOpen = false;
        _statusMessage = currentHighlight.IsExcluded ? "Highlight included." : "Highlight excluded.";
    }

    private async Task ToggleBookExclusionAsync(CancellationToken cancellationToken)
    {
        using var response = _isBookExcluded
            ? await _client.DeleteExcludeAsync("book", _bookId, cancellationToken).ConfigureAwait(false)
            : await _client.PostExcludeAsync("book", _bookId, cancellationToken).ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
        {
            _statusMessage = $"Book update failed: {(int)response.StatusCode} {response.ReasonPhrase}";
            return;
        }

        _isBookExcluded = !_isBookExcluded;
        ActionMenuOpen = false;
        _statusMessage = _isBookExcluded ? "Book excluded." : "Book included.";
    }

    private async Task ToggleAuthorExclusionAsync(CancellationToken cancellationToken)
    {
        using var response = _isAuthorExcluded
            ? await _client.DeleteExcludeAsync("author", _authorId, cancellationToken).ConfigureAwait(false)
            : await _client.PostExcludeAsync("author", _authorId, cancellationToken).ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
        {
            _statusMessage = $"Author update failed: {(int)response.StatusCode} {response.ReasonPhrase}";
            return;
        }

        _isAuthorExcluded = !_isAuthorExcluded;
        ActionMenuOpen = false;
        _statusMessage = _isAuthorExcluded ? "Author excluded." : "Author included.";
    }

    private async Task DeleteSelectedHighlightAsync(CancellationToken cancellationToken)
    {
        var currentHighlight = GetSelectedHighlight();
        if (currentHighlight is null)
        {
            DeleteConfirmationOpen = false;
            return;
        }

        using var response = await _client.DeleteHighlightAsync(currentHighlight.Id, cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            _statusMessage = $"Delete failed: {(int)response.StatusCode} {response.ReasonPhrase}";
            DeleteConfirmationOpen = false;
            return;
        }

        _highlights.RemoveAt(SelectedIndex);
        SelectedIndex = Math.Clamp(SelectedIndex, 0, Math.Max(0, _highlights.Count - 1));
        DeleteConfirmationOpen = false;
        _statusMessage = "Highlight deleted.";
    }

    private HighlightViewModel? GetSelectedHighlight()
        => SelectedIndex >= 0 && SelectedIndex < _highlights.Count ? _highlights[SelectedIndex] : null;

    private bool IsEffectivelyExcluded(HighlightViewModel highlight)
        => highlight.IsExcluded || _isBookExcluded || _isAuthorExcluded;

    private void MoveSelection(int delta)
    {
        if (_highlights.Count == 0)
        {
            SelectedIndex = 0;
            return;
        }

        SelectedIndex = Math.Clamp(SelectedIndex + delta, 0, _highlights.Count - 1);
        _statusMessage = null;
    }

    private List<string> GetActionLabels()
        =>
        [
            "Modify weight",
            GetSelectedHighlight()?.IsExcluded == true ? "Include highlight" : "Exclude highlight",
            _isBookExcluded ? "Include book" : "Exclude book",
            _isAuthorExcluded ? "Include author" : "Exclude author",
            "Delete highlight"
        ];

    private static string Truncate(string value, int maxLength)
    {
        if (value.Length <= maxLength)
        {
            return value;
        }

        return value[..(maxLength - 3)] + "...";
    }
}
