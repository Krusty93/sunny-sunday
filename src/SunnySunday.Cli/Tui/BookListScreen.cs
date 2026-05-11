using System.Collections.ObjectModel;
using System.Globalization;
using Terminal.Gui.Drawing;
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
    private const int DefaultTableWidth = 80;
    private const int HighlightsColumnWidth = 10;
    private const int MinimumTitleColumnWidth = 18;
    private const int MinimumAuthorColumnWidth = 18;
    private const int PreferredAuthorColumnWidth = 30;
    private const int TableHorizontalPadding = 2;
    private const string SectionTitle = "Books";
    private const string PlaceholderIdle = "type / to search";
    private const string PlaceholderFocused = "Press Esc to return to the list";
    private readonly SunnyHttpClient _client = client;
    private List<BookViewModel> _books = [];
    private List<BookViewModel> _filteredBooks = [];
    private int _selectedIndex;
    private bool _isSearchActive;
    private string _searchQuery = string.Empty;
    private FrameView? _searchFrame;
    private SearchTextField? _searchField;
    private Label? _searchPlaceholder;

    public IReadOnlyList<BookViewModel> Books => _books;

    public IReadOnlyList<BookViewModel> FilteredBooks => _filteredBooks;

    public int SelectedIndex => _selectedIndex;

    public bool IsSearchActive => _isSearchActive;

    public string SearchQuery => _searchQuery;

    public string Title => string.Empty;

    public IReadOnlyList<(string Key, string Label)> KeyHints =>
    [
        ("↑↓", "Navigate"),
        ("Enter", "View"),
        ("S", "Settings"),
        ("/", "Search"),
        ("R", "Refresh"),
        ("Q", "Quit")
    ];

    private readonly record struct TableLayout(int TitleWidth, int AuthorWidth, int HighlightsWidth);

    public int ToolbarHeight => 4;

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Reliability", "CA2000:Dispose objects before losing scope", Justification = "Views are owned by the window hierarchy")]
    public View? CreateToolbarView(Action<ScreenResult> navigate)
    {
        var container = new View
        {
            X = 0,
            Y = 0,
            Width = Dim.Fill(),
            Height = 4,
            CanFocus = true
        };

        _searchFrame = new FrameView
        {
            X = 0,
            Y = 0,
            Width = Dim.Fill(),
            Height = 3,
            BorderStyle = LineStyle.Rounded,
            Title = string.Empty,
            CanFocus = true
        };
        _searchFrame.SetScheme(CreateSearchFrameScheme(isFocused: false));

        var fieldAttribute = new Terminal.Gui.Drawing.Attribute(
            Terminal.Gui.Drawing.Color.White, StatusChrome.Background);

        _searchField = new SearchTextField
        {
            X = 1,
            Y = 0,
            Width = Dim.Fill(2),
            Height = 1,
            CanFocus = true,
            Text = _searchQuery
        };
        _searchField.SetScheme(CreateSearchFieldScheme(fieldAttribute));

        _searchPlaceholder = new Label
        {
            X = 2,
            Y = 0,
            Width = Dim.Fill(3),
            Height = 1,
            CanFocus = false,
            Text = PlaceholderIdle,
            Visible = string.IsNullOrEmpty(_searchQuery)
        };
        _searchPlaceholder.SetScheme(new Scheme(new Terminal.Gui.Drawing.Attribute(
            new Terminal.Gui.Drawing.Color(90, 90, 90), StatusChrome.Background)));

        _searchFrame.Add(_searchField, _searchPlaceholder);
        container.Add(_searchFrame);

        return container;
    }

    private static Scheme CreateSearchFrameScheme(bool isFocused)
    {
        var borderColor = isFocused
            ? new Terminal.Gui.Drawing.Color(110, 200, 255)
            : new Terminal.Gui.Drawing.Color(60, 100, 140);

        return new Scheme(new Terminal.Gui.Drawing.Attribute(borderColor, StatusChrome.Background))
        {
            Normal = new Terminal.Gui.Drawing.Attribute(borderColor, StatusChrome.Background),
            Focus = new Terminal.Gui.Drawing.Attribute(borderColor, StatusChrome.Background),
            Active = new Terminal.Gui.Drawing.Attribute(borderColor, StatusChrome.Background),
            HotNormal = new Terminal.Gui.Drawing.Attribute(borderColor, StatusChrome.Background),
            HotFocus = new Terminal.Gui.Drawing.Attribute(borderColor, StatusChrome.Background),
            HotActive = new Terminal.Gui.Drawing.Attribute(borderColor, StatusChrome.Background),
            Disabled = new Terminal.Gui.Drawing.Attribute(borderColor, StatusChrome.Background)
        };
    }

    private static Scheme CreateSearchFieldScheme(Terminal.Gui.Drawing.Attribute attribute) => new(attribute)
    {
        Normal = attribute,
        Focus = attribute,
        Active = attribute,
        Code = attribute,
        Editable = attribute,
        Highlight = attribute,
        HotActive = attribute,
        HotFocus = attribute,
        HotNormal = attribute,
        ReadOnly = attribute,
        Disabled = attribute
    };

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

        var tableLayout = CalculateTableLayout(DefaultTableWidth);

        var titleLabel = new Label
        {
            Text = SectionTitle,
            X = TableHorizontalPadding,
            Y = 0,
            Width = Dim.Fill(TableHorizontalPadding * 2)
        };
        titleLabel.SetScheme(new Scheme(new Terminal.Gui.Drawing.Attribute(
            new Terminal.Gui.Drawing.Color(110, 200, 255), StatusChrome.Background)));

        var headerLabel = new Label
        {
            Text = FormatHeader(tableLayout),
            X = TableHorizontalPadding,
            Y = 2,
            Width = Dim.Fill(TableHorizontalPadding * 2)
        };
        headerLabel.SetScheme(new Scheme(new Terminal.Gui.Drawing.Attribute(
            new Terminal.Gui.Drawing.Color(150, 190, 230), StatusChrome.Background)));

        var headerRuleLabel = new Label
        {
            X = TableHorizontalPadding,
            Y = 3,
            Width = Dim.Fill(TableHorizontalPadding * 2),
            Text = string.Empty
        };
        headerRuleLabel.SetScheme(new Scheme(new Terminal.Gui.Drawing.Attribute(
            new Terminal.Gui.Drawing.Color(60, 100, 140), StatusChrome.Background)));

        var displayItems = new ObservableCollection<string>(
            _filteredBooks.Select(book => FormatBookRow(book, tableLayout)));

        var listView = new ShortcutListView
        {
            X = TableHorizontalPadding,
            Y = 4,
            Width = Dim.Fill(TableHorizontalPadding * 2),
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
            foreach (var item in _filteredBooks.Select(book => FormatBookRow(book, tableLayout)))
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

        void UpdateTableLayout()
        {
            var availableWidth = Math.Max(listView.Viewport.Width, headerLabel.Viewport.Width);
            if (availableWidth <= 0)
            {
                return;
            }

            var nextLayout = CalculateTableLayout(availableWidth);
            if (nextLayout == tableLayout)
            {
                return;
            }

            tableLayout = nextLayout;
            headerLabel.Text = FormatHeader(tableLayout);
            headerRuleLabel.Text = new string('-', availableWidth);
            RefreshVisibleBooks();
        }

        void FocusSearchField()
        {
            _isSearchActive = true;
            _searchField?.SetFocus();
            UpdateSearchChrome();
        }

        void FocusListView()
        {
            _isSearchActive = false;
            if (_searchField is not null)
            {
                _searchQuery = _searchField.Text ?? string.Empty;
            }

            ApplyFilter();
            RefreshVisibleBooks();
            listView.SetFocus();
            UpdateSearchChrome();
        }

        void UpdateSearchChrome()
        {
            if (_searchPlaceholder is not null && _searchField is not null)
            {
                _searchPlaceholder.Visible = string.IsNullOrEmpty(_searchField.Text);
                _searchPlaceholder.Text = _searchField.HasFocus
                    ? PlaceholderFocused
                    : PlaceholderIdle;
            }

            if (_searchFrame is not null && _searchField is not null)
            {
                _searchFrame.SetScheme(CreateSearchFrameScheme(_searchField.HasFocus));
            }
        }

        if (_searchField is not null)
        {
            _searchField.TextChanged += (_, _) =>
            {
                _searchQuery = _searchField.Text ?? string.Empty;
                ApplyFilter();
                RefreshVisibleBooks();
                UpdateSearchChrome();
            };

            _searchField.HasFocusChanged += (_, _) => UpdateSearchChrome();

            _searchField.KeyDown += (_, key) =>
            {
                if (key.KeyCode is KeyCode.Esc or KeyCode.CursorDown)
                {
                    FocusListView();
                    key.Handled = true;
                }
            };
        }

        UpdateSearchChrome();
        UpdateTableLayout();

        container.SubViewsLaidOut += (_, _) => UpdateTableLayout();
        listView.ViewportChanged += (_, _) => UpdateTableLayout();

        container.Add(titleLabel, headerLabel, headerRuleLabel, listView);
        SetupContainerKeyBindings(container, listView, navigate, RefreshVisibleBooks, FocusSearchField);
        listView.SetFocus();

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
        ShortcutListView? listView,
        Action<ScreenResult> navigate,
        Action? refreshVisibleBooks,
        Action? focusSearchField)
    {
        void HandleShortcutKey(Key key)
        {
            if (key.Handled)
            {
                return;
            }

            var shortcutKey = GetShortcutKey(key);
            if (shortcutKey is null)
            {
                return;
            }

            if (TryHandleShortcutKey(shortcutKey.Value, navigate, refreshVisibleBooks, focusSearchField))
            {
                key.Handled = true;
            }
        }

        container.KeyDown += (_, key) => HandleShortcutKey(key);

        if (listView is not null)
        {
            // ShortcutKeyPressed fires inside OnKeyDown, BEFORE the CollectionNavigator
            listView.ShortcutKeyPressed += (_, key) => HandleShortcutKey(key);

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
        Action? focusSearchField)
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
                focusSearchField?.Invoke();
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

    private static TableLayout CalculateTableLayout(int availableWidth)
    {
        const int spacingWidth = 2;

        var textColumnsWidth = Math.Max(0, availableWidth - HighlightsColumnWidth - spacingWidth);
        if (textColumnsWidth == 0)
        {
            return new TableLayout(0, 0, HighlightsColumnWidth);
        }

        var authorWidth = Math.Min(
            PreferredAuthorColumnWidth,
            Math.Max(MinimumAuthorColumnWidth, textColumnsWidth / 3));

        var titleWidth = Math.Max(MinimumTitleColumnWidth, textColumnsWidth - authorWidth);
        if (titleWidth + authorWidth > textColumnsWidth)
        {
            authorWidth = Math.Max(0, textColumnsWidth - titleWidth);
        }

        if (textColumnsWidth >= MinimumTitleColumnWidth + MinimumAuthorColumnWidth)
        {
            authorWidth = Math.Max(MinimumAuthorColumnWidth, authorWidth);
            titleWidth = textColumnsWidth - authorWidth;
        }
        else
        {
            titleWidth = Math.Max(0, textColumnsWidth / 2);
            authorWidth = Math.Max(0, textColumnsWidth - titleWidth);
        }

        return new TableLayout(titleWidth, authorWidth, HighlightsColumnWidth);
    }

    private static string FormatHeader(TableLayout tableLayout)
    {
        return $"{FitCell("TITLE", tableLayout.TitleWidth)} {FitCell("AUTHOR", tableLayout.AuthorWidth)} {"HIGHLIGHTS".PadLeft(tableLayout.HighlightsWidth)}";
    }

    private static string FormatBookRow(BookViewModel book, TableLayout tableLayout)
    {
        var title = FitCell(book.Title, tableLayout.TitleWidth);
        var author = FitCell(book.Author, tableLayout.AuthorWidth);
        return $"{title} {author} {book.HighlightCount.ToString(CultureInfo.InvariantCulture).PadLeft(tableLayout.HighlightsWidth)}";
    }

    private static string FitCell(string value, int width)
    {
        if (width <= 0)
        {
            return string.Empty;
        }

        if (value.Length <= width)
        {
            return value.PadRight(width);
        }

        if (width <= 3)
        {
            return value[..width];
        }

        return value[..(width - 3)] + "...";
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
        _filteredBooks = string.IsNullOrEmpty(_searchQuery)
            ? [.. _books]
            : SearchFilter.Apply(_books, _searchQuery);

        if (_filteredBooks.Count == 0)
        {
            _selectedIndex = 0;
            return;
        }

        _selectedIndex = Math.Clamp(_selectedIndex, 0, _filteredBooks.Count - 1);
    }
}
