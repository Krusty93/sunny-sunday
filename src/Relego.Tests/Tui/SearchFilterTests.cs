using Relego.Cli.Tui;
using Relego.Cli.Tui.ViewModels;

namespace Relego.Tests.Tui;

public sealed class SearchFilterTests
{
    [Fact]
    public void Apply_EmptyQuery_ReturnsAllBooks()
    {
        var books = CreateBooks();

        var result = SearchFilter.Apply(books, string.Empty);

        Assert.Equal(books, result);
    }

    [Fact]
    public void Apply_MatchesByTitle()
    {
        var result = SearchFilter.Apply(CreateBooks(), "hobbit");

        var book = Assert.Single(result);
        Assert.Equal("The Hobbit", book.Title);
    }

    [Fact]
    public void Apply_MatchesByAuthor()
    {
        var result = SearchFilter.Apply(CreateBooks(), "asimov");

        var book = Assert.Single(result);
        Assert.Equal("Isaac Asimov", book.Author);
    }

    [Fact]
    public void Apply_MatchesByHighlightText()
    {
        var result = SearchFilter.Apply(CreateBooks(), "probability");

        var book = Assert.Single(result);
        Assert.Equal("Foundation", book.Title);
    }

    [Fact]
    public void Apply_IsCaseInsensitive()
    {
        var result = SearchFilter.Apply(CreateBooks(), "BAG END");

        var book = Assert.Single(result);
        Assert.Equal("The Hobbit", book.Title);
    }

    [Fact]
    public void Apply_PartialMatch_ReturnsMatchingBook()
    {
        var result = SearchFilter.Apply(CreateBooks(), "found");

        var book = Assert.Single(result);
        Assert.Equal("Foundation", book.Title);
    }

    [Fact]
    public void Apply_NoMatch_ReturnsEmptyList()
    {
        var result = SearchFilter.Apply(CreateBooks(), "dune");

        Assert.Empty(result);
    }

    private static List<BookViewModel> CreateBooks()
    {
        return
        [
            new BookViewModel(
                10,
                100,
                "The Hobbit",
                "J.R.R. Tolkien",
                2,
                false,
                false,
                [
                    new HighlightViewModel(1, 10, 100, "In a hole in the ground there lived a hobbit.", "The Hobbit", "J.R.R. Tolkien", false, null),
                    new HighlightViewModel(2, 10, 100, "Bag End was a comfortable tunnel.", "The Hobbit", "J.R.R. Tolkien", false, null)
                ]),
            new BookViewModel(
                20,
                200,
                "Foundation",
                "Isaac Asimov",
                1,
                false,
                false,
                [
                    new HighlightViewModel(3, 20, 200, "Violence is the last refuge of the incompetent, but probability guides the plan.", "Foundation", "Isaac Asimov", false, null)
                ])
        ];
    }
}
