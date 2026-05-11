using RichardSzalay.MockHttp;
using SunnySunday.Cli.Infrastructure;
using SunnySunday.Cli.Tui;

namespace SunnySunday.Tests.Tui;

public sealed class BookListScreenTests : IDisposable
{
    private readonly MockHttpMessageHandler _mockHttp = new();

    public void Dispose() => _mockHttp.Dispose();

    [Fact]
    public async Task MoveSelection_DownAndUp_MovesWithinBounds()
    {
        var screen = await CreateScreenAsync();

        screen.MoveSelection(1);
        screen.MoveSelection(1);

        Assert.Equal(1, screen.SelectedIndex);

        screen.MoveSelection(-1);
        screen.MoveSelection(-1);

        Assert.Equal(0, screen.SelectedIndex);
    }

    [Fact]
    public async Task GetSelectedBook_ReturnsFirstBookByDefault()
    {
        var screen = await CreateScreenAsync();

        var selected = screen.GetSelectedBook();

        Assert.NotNull(selected);
        Assert.Equal("Foundation", selected.Title);
    }

    [Fact]
    public async Task ActivateSearch_SetsSearchModeActive()
    {
        var screen = await CreateScreenAsync();

        screen.ActivateSearch();

        Assert.True(screen.IsSearchActive);
    }

    [Fact]
    public async Task EmptyBookList_HasNoFilteredBooks()
    {
        var screen = await CreateScreenAsync(total: 0, itemsJson: "[]");

        Assert.Empty(screen.Books);
        Assert.Empty(screen.FilteredBooks);
    }

    [Fact]
    public async Task InitializeAsync_EnrichesBooksWithExclusionsAndWeights()
    {
        var screen = await CreateScreenAsync();

        var foundation = Assert.Single(screen.Books, book => book.Title == "Foundation");
        Assert.True(foundation.IsAuthorExcluded);
        Assert.False(foundation.IsBookExcluded);
        Assert.All(foundation.Highlights, highlight => Assert.False(highlight.IsExcluded));
        Assert.Contains(foundation.Highlights, highlight => highlight.Id == 1 && highlight.Weight == 5);
        Assert.Contains(foundation.Highlights, highlight => highlight.Id == 2 && highlight.Weight is null);
    }

    [Fact]
    public async Task TryHandleShortcutKey_Q_RequestsQuit()
    {
        var screen = await CreateScreenAsync();
        ScreenResult? result = null;

        var handled = screen.TryHandleShortcutKey('q', navigate => result = navigate, null, null);

        Assert.True(handled);
        Assert.NotNull(result);
        Assert.Equal(ScreenAction.Quit, result!.Action);
    }

    [Fact]
    public async Task TryHandleShortcutKey_Slash_FocusesSearchField()
    {
        var screen = await CreateScreenAsync();
        var focusedSearchField = false;

        var handled = screen.TryHandleShortcutKey('/', _ => { }, null, () => focusedSearchField = true);

        Assert.True(handled);
        Assert.True(focusedSearchField);
    }

    [Fact]
    public async Task TryHandleShortcutKey_R_ReinitializesBooks()
    {
        const string initialItemsJson = """
            [
              {
                "id": 1,
                "text": "Psychohistory is built on large numbers.",
                "bookTitle": "Foundation",
                "authorName": "Isaac Asimov"
              }
            ]
            """;

        using var mockHttp = new MockHttpMessageHandler(BackendDefinitionBehavior.Always);

        mockHttp.Expect(HttpMethod.Get, "http://localhost:5000/highlights?page=1&pageSize=100")
            .Respond("application/json", $$"""
                {
                  "total": 1,
                  "page": 1,
                  "pageSize": 100,
                  "items": {{initialItemsJson}}
                }
                """);

        mockHttp.Expect(HttpMethod.Get, "http://localhost:5000/highlights?page=1&pageSize=100")
            .Respond("application/json", """
                {
                  "total": 0,
                  "page": 1,
                  "pageSize": 100,
                  "items": []
                }
                """);

        ConfigureSupplementaryEndpoints(mockHttp);

        var httpClient = mockHttp.ToHttpClient();
        httpClient.BaseAddress = new Uri("http://localhost:5000");

        var screen = new BookListScreen(new SunnyHttpClient(httpClient));
        await screen.InitializeAsync(CancellationToken.None);

        var refreshedVisibleBooks = false;
        var handled = screen.TryHandleShortcutKey('r', _ => { }, () => refreshedVisibleBooks = true, null);

        Assert.True(handled);
        Assert.Empty(screen.Books);
        Assert.True(refreshedVisibleBooks);
        mockHttp.VerifyNoOutstandingExpectation();
    }

    private async Task<BookListScreen> CreateScreenAsync(int total = 3, string? itemsJson = null)
    {
        itemsJson ??= """
            [
              {
                "id": 1,
                "bookId": 10,
                "authorId": 7,
                "text": "Psychohistory is built on large numbers.",
                "bookTitle": "Foundation",
                "authorName": "Isaac Asimov"
              },
              {
                "id": 2,
                "bookId": 10,
                "authorId": 7,
                "text": "Violence is the last refuge of the incompetent.",
                "bookTitle": "Foundation",
                "authorName": "Isaac Asimov"
              },
              {
                "id": 3,
                "bookId": 20,
                "authorId": 8,
                "text": "In a hole in the ground there lived a hobbit.",
                "bookTitle": "The Hobbit",
                "authorName": "J.R.R. Tolkien"
              }
            ]
            """;

        _mockHttp.When(HttpMethod.Get, "http://localhost:5000/highlights?page=1&pageSize=100")
            .Respond("application/json", $$"""
                {
                  "total": {{total}},
                  "page": 1,
                  "pageSize": 100,
                  "items": {{itemsJson}}
                }
                """);

        ConfigureSupplementaryEndpoints(_mockHttp);

        var httpClient = _mockHttp.ToHttpClient();
        httpClient.BaseAddress = new Uri("http://localhost:5000");

        var screen = new BookListScreen(new SunnyHttpClient(httpClient));
        await screen.InitializeAsync(CancellationToken.None);
        return screen;
    }

    private static void ConfigureSupplementaryEndpoints(MockHttpMessageHandler mockHttp)
    {
        mockHttp.When(HttpMethod.Get, "http://localhost:5000/exclusions")
            .Respond("application/json", """
                {
                  "highlights": [],
                  "books": [],
                  "authors": [
                    {
                      "id": 7,
                      "name": "Isaac Asimov",
                      "bookCount": 1
                    }
                  ]
                }
                """);

        mockHttp.When(HttpMethod.Get, "http://localhost:5000/highlights/weights")
            .Respond("application/json", """
                [
                  {
                    "id": 1,
                    "text": "Psychohistory is built on large numbers.",
                    "bookTitle": "Foundation",
                    "weight": 5
                  }
                ]
                """);
    }
}
