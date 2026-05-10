using System.Globalization;
using Spectre.Console;
using Spectre.Console.Rendering;
using SunnySunday.Cli.Infrastructure;
using SunnySunday.Cli.Tui.ViewModels;

namespace SunnySunday.Cli.Tui;

public sealed class BookListScreen(SunnyHttpClient client) : IScreen
{
    private const int PageSize = 100;
    private readonly SunnyHttpClient _client = client;
    private List<BookViewModel> _books = [];
    private List<BookViewModel> _filteredBooks = [];
    private int _selectedIndex;
    private bool _isSearchActive;
    private string _searchQuery = string.Empty;

    public IReadOnlyList<BookViewModel> Books => _books;

    public IReadOnlyList<BookViewModel> FilteredBooks => _filteredBooks;

    public int SelectedIndex => _selectedIndex;

    public bool IsSearchActive => _isSearchActive;

    public string SearchQuery => _searchQuery;

    public string KeyHints => "[↑↓] Navigate · [Enter] View · [S] Settings · [/] Search · [R] Refresh · [Q] Quit";

    public async Task InitializeAsync(CancellationToken cancellationToken)
    {
        var highlights = await LoadAllHighlightsAsync(cancellationToken).ConfigureAwait(false);
        var exclusions = await _client.GetExclusionsAsync(cancellationToken).ConfigureAwait(false);
        var weights = await _client.GetWeightsAsync(cancellationToken).ConfigureAwait(false);

        _books = EnrichBooks(BookViewModel.FromHighlights(highlights), exclusions, weights);
        ApplyFilter();
    }

    public IRenderable Render()
    {
        var body = BuildBody();
        if (!_isSearchActive)
        {
            return body;
        }

        return new Rows(BuildSearchPrompt(), body);
    }

    public async Task<ScreenResult> HandleKeyAsync(ConsoleKeyInfo key, CancellationToken cancellationToken)
    {
        if (HandleSearchModeKey(key))
        {
            return ScreenResult.Stay();
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
                return GetSelectedBook() is { } selectedBook
                    ? ScreenResult.Push(new HighlightDetailScreen(selectedBook, _client))
                    : ScreenResult.Stay();
            case ConsoleKey.S:
                return ScreenResult.Push(new SettingsScreen(_client));
            case ConsoleKey.R:
                await InitializeAsync(cancellationToken).ConfigureAwait(false);
                return ScreenResult.Stay();
            case ConsoleKey.Q:
                return ScreenResult.Quit();
            case ConsoleKey.C when key.Modifiers.HasFlag(ConsoleModifiers.Control):
                return ScreenResult.Quit();
            default:
                if (key.KeyChar == '/')
                {
                    _isSearchActive = true;
                    ApplyFilter();
                }

                return ScreenResult.Stay();
        }
    }

    private bool HandleSearchModeKey(ConsoleKeyInfo key)
    {
        if (!_isSearchActive)
        {
            return false;
        }

        if (key.Key == ConsoleKey.Escape)
        {
            _isSearchActive = false;
            _searchQuery = string.Empty;
            ApplyFilter();
            return true;
        }

        if (key.Key == ConsoleKey.Backspace)
        {
            if (_searchQuery.Length > 0)
            {
                _searchQuery = _searchQuery[..^1];
                ApplyFilter();
            }

            return true;
        }

        if (key.KeyChar != '/' && !char.IsControl(key.KeyChar) && key.Modifiers == 0)
        {
            _searchQuery += key.KeyChar;
            ApplyFilter();
            return true;
        }

        return false;
    }

    private async Task<List<SunnySunday.Core.Contracts.HighlightItemDto>> LoadAllHighlightsAsync(CancellationToken cancellationToken)
    {
        var highlights = new List<SunnySunday.Core.Contracts.HighlightItemDto>();
        var page = 1;

        while (true)
        {
            var response = await _client.GetHighlightsAsync(page, PageSize, null, cancellationToken).ConfigureAwait(false);
            if (response.Items.Count == 0)
            {
                break;
            }

            highlights.AddRange(response.Items);
            if (highlights.Count >= response.Total)
            {
                break;
            }

            page++;
        }

        return highlights;
    }

    private static List<BookViewModel> EnrichBooks(
        IEnumerable<BookViewModel> books,
        SunnySunday.Core.Contracts.ExclusionsResponse exclusions,
        IEnumerable<SunnySunday.Core.Contracts.WeightedHighlightDto> weights)
    {
        var excludedHighlightIds = exclusions.Highlights.Select(highlight => highlight.Id).ToHashSet();
        var excludedBooks = exclusions.Books
            .Select(book => CreateBookKey(book.Title, book.AuthorName))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var excludedAuthors = exclusions.Authors
            .Select(author => author.Name)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var weightLookup = weights.ToDictionary(weight => weight.Id, weight => weight.Weight);

        return books
            .Select(book =>
            {
                var isBookExcluded = excludedBooks.Contains(CreateBookKey(book.Title, book.Author));
                var isAuthorExcluded = excludedAuthors.Contains(book.Author);
                var highlights = book.Highlights
                    .Select(highlight => new HighlightViewModel(
                        highlight.Id,
                        highlight.Text,
                        highlight.BookTitle,
                        highlight.AuthorName,
                        excludedHighlightIds.Contains(highlight.Id) || isBookExcluded || isAuthorExcluded,
                        weightLookup.TryGetValue(highlight.Id, out var weight) ? weight : null))
                    .ToList();

                return new BookViewModel(book.Title, book.Author, highlights.Count, highlights);
            })
            .ToList();
    }

    private static string CreateBookKey(string title, string author) => $"{title}|{author}";

    private IRenderable BuildBody()
    {
        if (_filteredBooks.Count == 0)
        {
            var message = _isSearchActive && !string.IsNullOrWhiteSpace(_searchQuery)
                ? $"[yellow]No results matching '{Markup.Escape(_searchQuery)}'.[/]"
                : "[grey]No books imported yet.[/]";

            return new Panel(new Markup(message))
            {
                Header = new PanelHeader("Books")
            };
        }

        var table = new Table()
            .Border(TableBorder.Rounded)
            .Expand();
        table.AddColumn("Title");
        table.AddColumn("Author");
        table.AddColumn(new TableColumn("Highlights").RightAligned());

        for (var index = 0; index < _filteredBooks.Count; index++)
        {
            var book = _filteredBooks[index];
            var isSelected = index == _selectedIndex;

            table.AddRow(
                new Markup(FormatCell(book.Title, isSelected)),
                new Markup(FormatCell(book.Author, isSelected)),
                new Markup(FormatCell(book.HighlightCount.ToString(CultureInfo.InvariantCulture), isSelected)));
        }

        return table;
    }

    private IRenderable BuildSearchPrompt()
    {
        var query = string.IsNullOrEmpty(_searchQuery) ? string.Empty : Markup.Escape(_searchQuery);
        return new Markup($"[grey]Search:[/] [white]{query}[/]");
    }

    private static string FormatCell(string value, bool isSelected)
    {
        var escaped = Markup.Escape(value);
        return isSelected ? $"[black on yellow]{escaped}[/]" : escaped;
    }

    private void MoveSelection(int delta)
    {
        if (_filteredBooks.Count == 0)
        {
            _selectedIndex = 0;
            return;
        }

        _selectedIndex = Math.Clamp(_selectedIndex + delta, 0, _filteredBooks.Count - 1);
    }

    private void ApplyFilter()
    {
        _filteredBooks = _isSearchActive
            ? SearchFilter.Apply(_books, _searchQuery)
            : [.. _books];

        if (_filteredBooks.Count == 0)
        {
            _selectedIndex = 0;
            return;
        }

        _selectedIndex = Math.Clamp(_selectedIndex, 0, _filteredBooks.Count - 1);
    }

    private BookViewModel? GetSelectedBook()
    {
        if (_filteredBooks.Count == 0)
        {
            return null;
        }

        return _filteredBooks[_selectedIndex];
    }
}
