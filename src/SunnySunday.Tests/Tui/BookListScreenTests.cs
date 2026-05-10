using RichardSzalay.MockHttp;
using Spectre.Console;
using SunnySunday.Cli.Infrastructure;
using SunnySunday.Cli.Tui;

namespace SunnySunday.Tests.Tui;

public sealed class BookListScreenTests : IDisposable
{
    private readonly MockHttpMessageHandler _mockHttp = new();

    public void Dispose() => _mockHttp.Dispose();

    [Fact]
    public async Task HandleKeyAsync_DownAndUp_MoveSelectionWithinBounds()
    {
        var screen = await CreateScreenAsync();

        await screen.HandleKeyAsync(new ConsoleKeyInfo('\0', ConsoleKey.DownArrow, false, false, false), CancellationToken.None);
        await screen.HandleKeyAsync(new ConsoleKeyInfo('\0', ConsoleKey.DownArrow, false, false, false), CancellationToken.None);

        Assert.Equal(1, screen.SelectedIndex);

        await screen.HandleKeyAsync(new ConsoleKeyInfo('\0', ConsoleKey.UpArrow, false, false, false), CancellationToken.None);
        await screen.HandleKeyAsync(new ConsoleKeyInfo('\0', ConsoleKey.UpArrow, false, false, false), CancellationToken.None);

        Assert.Equal(0, screen.SelectedIndex);
    }

    [Fact]
    public async Task HandleKeyAsync_Enter_PushesHighlightDetailScreen()
    {
        var screen = await CreateScreenAsync();

        var result = await screen.HandleKeyAsync(new ConsoleKeyInfo('\r', ConsoleKey.Enter, false, false, false), CancellationToken.None);

        Assert.Equal(ScreenAction.Push, result.Action);
        var detailScreen = Assert.IsType<HighlightDetailScreen>(result.Next);
        Assert.Equal("Foundation", detailScreen.Book.Title);
    }

    [Fact]
    public async Task HandleKeyAsync_Q_ReturnsQuit()
    {
        var screen = await CreateScreenAsync();

        var result = await screen.HandleKeyAsync(new ConsoleKeyInfo('q', ConsoleKey.Q, false, false, false), CancellationToken.None);

        Assert.Equal(ScreenAction.Quit, result.Action);
    }

    [Fact]
    public async Task HandleKeyAsync_Slash_ActivatesSearchMode()
    {
        var screen = await CreateScreenAsync();

        await screen.HandleKeyAsync(new ConsoleKeyInfo('/', ConsoleKey.Oem2, false, false, false), CancellationToken.None);

        Assert.True(screen.IsSearchActive);
    }

    [Fact]
    public async Task Render_NoBooksLoaded_ReturnsEmptyStatePanel()
    {
        var screen = await CreateScreenAsync(total: 0, itemsJson: "[]");

        var renderable = screen.Render();

        Assert.IsType<Panel>(renderable);
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

        _mockHttp.When(HttpMethod.Get, "http://localhost:5000/exclusions")
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

        _mockHttp.When(HttpMethod.Get, "http://localhost:5000/highlights/weights")
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

        var httpClient = _mockHttp.ToHttpClient();
        httpClient.BaseAddress = new Uri("http://localhost:5000");

        var screen = new BookListScreen(new SunnyHttpClient(httpClient));
        await screen.InitializeAsync(CancellationToken.None);
        return screen;
    }
}
