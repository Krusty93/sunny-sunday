using System.Collections.ObjectModel;
using System.Globalization;
using Terminal.Gui.Drivers;
using Terminal.Gui.Input;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;
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

    public string Title => "Books";

    public IReadOnlyList<(string Key, string Label)> KeyHints =>
    [
        ("↑↓", "Navigate"),
        ("Enter", "View"),
        ("S", "Settings"),
        ("/", "Search"),
        ("R", "Refresh"),
        ("Q", "Quit")
    ];

    public async Task InitializeAsync(CancellationToken cancellationToken)
    {
        var highlights = await LoadAllHighlightsAsync(cancellationToken).ConfigureAwait(false);
        var exclusions = await _client.GetExclusionsAsync(cancellationToken).ConfigureAwait(false);
        var weights = await _client.GetWeightsAsync(cancellationToken).ConfigureAwait(false);

        _books = EnrichBooks(BookViewModel.FromHighlights(highlights), exclusions, weights);
        ApplyFilter();
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Reliability", "CA2000:Dispose objects before losing scope", Justification = "Views are owned by the parent container hierarchy")]
    public View CreateView(Action<ScreenResult> navigate)
    {
        var container = new View
        {
            X = 0,
            Y = 0,
            Width = Dim.Fill(),
            Height = Dim.Fill(),
            CanFocus = true
        };

        if (_filteredBooks.Count == 0 && !_isSearchActive)
        {
            var emptyLabel = new Label
            {
                Text = "No books imported yet. Run `sunny sync` to import.",
                X = Pos.Center(),
                Y = Pos.Center()
            };
            container.Add(emptyLabel);
            SetupContainerKeyBindings(container, null, navigate, null, null);
            // Views are owned by the container hierarchy
            return container;
        }

        var searchField = new TextField
        {
            X = 0,
            Y = 0,
            Width = Dim.Fill(),
            Visible = _isSearchActive,
            Text = _searchQuery
        };

        var headerLabel = new Label
        {
            Text = FormatHeader(),
            X = 0,
            Y = _isSearchActive ? 1 : 0,
            Width = Dim.Fill()
        };

        var displayItems = new ObservableCollection<string>(
            _filteredBooks.Select(FormatBookRow));

        var listView = new ListView
        {
            X = 0,
            Y = _isSearchActive ? 2 : 1,
            Width = Dim.Fill(),
            Height = Dim.Fill(),
            CanFocus = true
        };
        listView.SetSource(displayItems);

        if (_filteredBooks.Count > 0)
        {
            listView.SelectedItem = Math.Min(_selectedIndex, _filteredBooks.Count - 1);
        }

        listView.Accepting += (_, args) =>
        {
            var selected = GetSelectedBook();
            if (selected is not null)
            {
                navigate(ScreenResult.Push(new HighlightDetailScreen(selected, _client)));
            }
        };

        void RefreshVisibleBooks()
        {
            displayItems.Clear();
            foreach (var item in _filteredBooks.Select(FormatBookRow))
            {
                displayItems.Add(item);
            }

            if (_filteredBooks.Count > 0)
            {
                listView.SelectedItem = Math.Min(_selectedIndex, _filteredBooks.Count - 1);
            }
            else
            {
                _selectedIndex = 0;
            }
        }

        void SetSearchLayout(bool searchActive)
        {
            searchField.Visible = searchActive;
            headerLabel.Y = searchActive ? 1 : 0;
            listView.Y = searchActive ? 2 : 1;
        }

        void ActivateSearchUi()
        {
            SetSearchLayout(true);
            searchField.Text = string.Empty;
            RefreshVisibleBooks();
            searchField.SetFocus();
        }

        void DeactivateSearchUi()
        {
            DeactivateSearch();
            SetSearchLayout(false);
            searchField.Text = string.Empty;
            RefreshVisibleBooks();
            listView.SetFocus();
        }

        searchField.TextChanged += (_, args) =>
        {
            _searchQuery = searchField.Text ?? string.Empty;
            ApplyFilter();
            RefreshVisibleBooks();
        };

        searchField.KeyDown += (_, key) =>
        {
            if (key.KeyCode == KeyCode.Esc)
            {
                DeactivateSearchUi();
                key.Handled = true;
            }
        };

        container.Add(searchField, headerLabel, listView);
        SetupContainerKeyBindings(container, listView, navigate, RefreshVisibleBooks, ActivateSearchUi);

        if (_isSearchActive)
        {
            searchField.SetFocus();
        }
        else
        {
            listView.SetFocus();
        }

        return container;
    }

    public void MoveSelection(int delta)
    {
        if (_filteredBooks.Count == 0)
        {
            _selectedIndex = 0;
            return;
        }

        _selectedIndex = Math.Clamp(_selectedIndex + delta, 0, _filteredBooks.Count - 1);
    }

    public void ActivateSearch()
    {
        _isSearchActive = true;
        ApplyFilter();
    }

    public void DeactivateSearch()
    {
        _isSearchActive = false;
        _searchQuery = string.Empty;
        ApplyFilter();
    }

    public BookViewModel? GetSelectedBook()
    {
        if (_filteredBooks.Count == 0)
        {
            return null;
        }

        _selectedIndex = Math.Clamp(_selectedIndex, 0, _filteredBooks.Count - 1);
        return _filteredBooks[_selectedIndex];
    }

    private void SetupContainerKeyBindings(
        View container,
        ListView? listView,
        Action<ScreenResult> navigate,
        Action? refreshVisibleBooks,
        Action? activateSearchUi)
    {
        void HandleGlobalKey(Key key)
        {
            if (key.Handled)
            {
                return;
            }

            if (_isSearchActive)
            {
                return;
            }

            var shortcutKey = GetShortcutKey(key);
            if (shortcutKey is null)
            {
                return;
            }

            if (TryHandleShortcutKey(shortcutKey.Value, navigate, refreshVisibleBooks, activateSearchUi))
            {
                key.Handled = true;
            }
        }

        container.KeyDown += (_, key) => HandleGlobalKey(key);

        if (listView is not null)
        {
            listView.KeyDown += (_, key) => HandleGlobalKey(key);

            listView.ValueChanged += (_, args) =>
            {
                _selectedIndex = listView.SelectedItem ?? 0;
            };
        }
    }

    public bool TryHandleShortcutKey(
        char shortcutKey,
        Action<ScreenResult> navigate,
        Action? refreshVisibleBooks,
        Action? activateSearchUi)
    {
        switch (char.ToLowerInvariant(shortcutKey))
        {
            case 'q':
                navigate(ScreenResult.Quit());
                return true;

            case 's':
                navigate(ScreenResult.Push(new SettingsScreen(_client)));
                return true;

            case 'r':
                InitializeAsync(CancellationToken.None).GetAwaiter().GetResult();
                refreshVisibleBooks?.Invoke();
                return true;

            case '/':
                ActivateSearch();
                activateSearchUi?.Invoke();
                return true;

            default:
                return false;
        }
    }

    private static char? GetShortcutKey(Key key)
    {
        return key.AsRune.Value switch
        {
            >= 'a' and <= 'z' => (char)key.AsRune.Value,
            >= 'A' and <= 'Z' => char.ToLowerInvariant((char)key.AsRune.Value),
            '/' => '/',
            _ => key.KeyCode switch
            {
                KeyCode.Q => 'q',
                KeyCode.R => 'r',
                KeyCode.S => 's',
                _ => null
            }
        };
    }

    private static string FormatHeader()
    {
        return $"{"Title",-40} {"Author",-30} {"Highlights",10}";
    }

    private static string FormatBookRow(BookViewModel book)
    {
        var title = book.Title.Length > 38 ? book.Title[..35] + "..." : book.Title;
        var author = book.Author.Length > 28 ? book.Author[..25] + "..." : book.Author;
        return $"{title,-40} {author,-30} {book.HighlightCount.ToString(CultureInfo.InvariantCulture),10}";
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
}
