using SunnySunday.Cli.Tui.ViewModels;
using SunnySunday.Core.Contracts;

namespace SunnySunday.Tests.Tui;

public sealed class BookGroupingTests
{
    [Fact]
    public void FromHighlights_GroupsMultipleHighlightsFromSameBook()
    {
        var result = BookViewModel.FromHighlights([
            new HighlightItemDto { Id = 1, BookId = 10, AuthorId = 100, Text = "One", BookTitle = "Book A", AuthorName = "Author A" },
            new HighlightItemDto { Id = 2, BookId = 10, AuthorId = 100, Text = "Two", BookTitle = "Book A", AuthorName = "Author A" }
        ]);

        var book = Assert.Single(result);
        Assert.Equal(10, book.BookId);
        Assert.Equal(100, book.AuthorId);
        Assert.Equal("Book A", book.Title);
        Assert.Equal("Author A", book.Author);
        Assert.Equal(2, book.HighlightCount);
        Assert.Equal(2, book.Highlights.Count);
    }

    [Fact]
    public void FromHighlights_SeparatesBooksByTitleAndAuthor()
    {
        var result = BookViewModel.FromHighlights([
            new HighlightItemDto { Id = 1, BookId = 10, AuthorId = 100, Text = "One", BookTitle = "Shared", AuthorName = "Author A" },
            new HighlightItemDto { Id = 2, BookId = 20, AuthorId = 200, Text = "Two", BookTitle = "Shared", AuthorName = "Author B" },
            new HighlightItemDto { Id = 3, BookId = 30, AuthorId = 300, Text = "Three", BookTitle = "Other", AuthorName = "Author C" }
        ]);

        Assert.Equal(3, result.Count);
        Assert.Contains(result, book => book.Title == "Shared" && book.Author == "Author A");
        Assert.Contains(result, book => book.Title == "Shared" && book.Author == "Author B");
        Assert.Contains(result, book => book.Title == "Other" && book.Author == "Author C");
    }

    [Fact]
    public void FromHighlights_EmptyInput_ReturnsEmptyList()
    {
        var result = BookViewModel.FromHighlights([]);

        Assert.Empty(result);
    }
}
