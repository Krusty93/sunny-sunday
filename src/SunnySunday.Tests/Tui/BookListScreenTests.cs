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
        Assert.All(foundation.Highlights, highlight => Assert.True(highlight.IsExcluded));
        Assert.Contains(foundation.Highlights, highlight => highlight.Id == 1 && highlight.Weight == 5);
        Assert.Contains(foundation.Highlights, highlight => highlight.Id == 2 && highlight.Weight is null);
    }

    private async Task<BookListScreen> CreateScreenAsync(int total = 3, string? itemsJson = null)
    {
        itemsJson ??= """
            [
              {
                "id": 1,
                "text": "Psychohistory is built on large numbers.",
                "bookTitle": "Foundation",
                "authorName": "Isaac Asimov"
              },
              {
                "id": 2,
                "text": "Violence is the last refuge of the incompetent.",
                "bookTitle": "Foundation",
                "authorName": "Isaac Asimov"
              },
              {
                "id": 3,
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
