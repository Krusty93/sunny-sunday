using SunnySunday.Cli.Parsing;

namespace SunnySunday.Tests.Parsing;

public class ClippingsParserTests
{
    #region T009 — Standard multi-book parsing

    [Fact]
    public async Task ParseAsync_StandardInput_ReturnsCorrectCountsAndHighlights()
    {
        // Arrange — 3 highlights across 2 books
        var input = """
            The Great Gatsby (F. Scott Fitzgerald)
            - Your Highlight on Location 100-105 | Added on Thursday, January 1, 2026 12:00:00 AM

            In my younger and more vulnerable years my father gave me some advice.
            ==========
            1984 (Orwell, George)
            - Your Highlight on Location 200-210 | Added on Friday, January 2, 2026 1:30:00 PM

            War is peace. Freedom is slavery. Ignorance is strength.
            ==========
            The Great Gatsby (F. Scott Fitzgerald)
            - Your Highlight on Location 150-160 | Added on Saturday, January 3, 2026 9:00:00 AM

            So we beat on, boats against the current, borne back ceaselessly into the past.
            ==========
            """;

        using var reader = new StringReader(input);

        // Act
        var result = await ClippingsParser.ParseAsync(reader);

        // Assert
        Assert.Equal(3, result.TotalEntriesProcessed);
        Assert.Equal(2, result.Books.Count);

        var gatsby = result.Books.Single(b => b.Title == "The Great Gatsby");
        Assert.Equal("F. Scott Fitzgerald", gatsby.Author);
        Assert.Equal(2, gatsby.Highlights.Count);
        Assert.Equal("In my younger and more vulnerable years my father gave me some advice.", gatsby.Highlights[0].Text);
        Assert.Equal("So we beat on, boats against the current, borne back ceaselessly into the past.", gatsby.Highlights[1].Text);

        var orwell = result.Books.Single(b => b.Title == "1984");
        Assert.Equal("Orwell, George", orwell.Author);
        Assert.Single(orwell.Highlights);
        Assert.Equal("War is peace. Freedom is slavery. Ignorance is strength.", orwell.Highlights[0].Text);
    }

    #endregion

    #region T010 — Multi-line highlight text

    [Fact]
    public async Task ParseAsync_MultiLineHighlight_PreservesNewlines()
    {
        var input = """
            Some Book (Author)
            - Your Highlight on Location 10-20 | Added on Thursday, January 1, 2026 12:00:00 AM

            First line of highlight.
            Second line continues here.
            Third line ends the highlight.
            ==========
            """;

        using var reader = new StringReader(input);
        var result = await ClippingsParser.ParseAsync(reader);

        Assert.Single(result.Books);
        var highlight = result.Books[0].Highlights[0];
        Assert.Contains("First line of highlight.", highlight.Text);
        Assert.Contains("Second line continues here.", highlight.Text);
        Assert.Contains("Third line ends the highlight.", highlight.Text);
        Assert.Contains("\n", highlight.Text);
    }

    #endregion

    #region T011 — Title/author extraction edge cases

    [Theory]
    [InlineData("Book Title (Author Name)", "Book Title", "Author Name")]
    [InlineData("1984 (Orwell, George)", "1984", "Orwell, George")]
    public async Task ParseAsync_StandardAuthorFormats_ExtractsCorrectly(
        string titleLine, string expectedTitle, string expectedAuthor)
    {
        var input = $"""
            {titleLine}
            - Your Highlight on Location 10-20 | Added on Thursday, January 1, 2026 12:00:00 AM

            Some text.
            ==========
            """;

        using var reader = new StringReader(input);
        var result = await ClippingsParser.ParseAsync(reader);

        Assert.Single(result.Books);
        Assert.Equal(expectedTitle, result.Books[0].Title);
        Assert.Equal(expectedAuthor, result.Books[0].Author);
    }

    [Fact]
    public async Task ParseAsync_TitleWithNestedParens_ExtractsLastParenGroupAsAuthor()
    {
        var input = """
            The Art of War (Annotated) (Sun Tzu)
            - Your Highlight on Location 10-20 | Added on Thursday, January 1, 2026 12:00:00 AM

            Some text.
            ==========
            """;

        using var reader = new StringReader(input);
        var result = await ClippingsParser.ParseAsync(reader);

        Assert.Equal("The Art of War (Annotated)", result.Books[0].Title);
        Assert.Equal("Sun Tzu", result.Books[0].Author);
    }

    [Fact]
    public async Task ParseAsync_NoParentheses_TitleIsFullLineAuthorIsNull()
    {
        var input = """
            My Personal Notes
            - Your Highlight on Location 10-20 | Added on Thursday, January 1, 2026 12:00:00 AM

            Some text.
            ==========
            """;

        using var reader = new StringReader(input);
        var result = await ClippingsParser.ParseAsync(reader);

        Assert.Equal("My Personal Notes", result.Books[0].Title);
        Assert.Null(result.Books[0].Author);
    }

    [Fact]
    public async Task ParseAsync_EmptyParens_AuthorIsNull()
    {
        var input = """
            Some Book ()
            - Your Highlight on Location 10-20 | Added on Thursday, January 1, 2026 12:00:00 AM

            Some text.
            ==========
            """;

        using var reader = new StringReader(input);
        var result = await ClippingsParser.ParseAsync(reader);

        Assert.Equal("Some Book", result.Books[0].Title);
        Assert.Null(result.Books[0].Author);
    }

    #endregion

    #region T012 — Metadata line parsing and entry type handling

    [Fact]
    public async Task ParseAsync_HighlightEntry_ExtractsLocationAndDate()
    {
        var input = """
            Book (Author)
            - Your Highlight on Location 100-105 | Added on Thursday, January 1, 2026 12:00:00 AM

            Highlighted text here.
            ==========
            """;

        using var reader = new StringReader(input);
        var result = await ClippingsParser.ParseAsync(reader);

        var h = result.Books[0].Highlights[0];
        Assert.Equal("Highlighted text here.", h.Text);
        Assert.Equal("Location 100-105", h.Location);
        Assert.NotNull(h.AddedOn);
        Assert.Equal(new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero), h.AddedOn);
    }

    [Fact]
    public async Task ParseAsync_NoteEntry_PrependsMyNotePrefix()
    {
        var input = """
            Book (Author)
            - Your Note on Location 50-55 | Added on Thursday, January 1, 2026 12:00:00 AM

            This is my annotation.
            ==========
            """;

        using var reader = new StringReader(input);
        var result = await ClippingsParser.ParseAsync(reader);

        var h = result.Books[0].Highlights[0];
        Assert.Equal("[my note] This is my annotation.", h.Text);
    }

    [Fact]
    public async Task ParseAsync_BookmarkEntry_IsExcludedFromResult()
    {
        var input = """
            Book (Author)
            - Your Bookmark on Location 30 | Added on Thursday, January 1, 2026 12:00:00 AM

            
            ==========
            Book (Author)
            - Your Highlight on Location 100-105 | Added on Thursday, January 1, 2026 12:00:00 AM

            Actual highlight text.
            ==========
            """;

        using var reader = new StringReader(input);
        var result = await ClippingsParser.ParseAsync(reader);

        Assert.Equal(2, result.TotalEntriesProcessed);
        Assert.Single(result.Books);
        Assert.Single(result.Books[0].Highlights);
        Assert.Equal("Actual highlight text.", result.Books[0].Highlights[0].Text);
    }

    #endregion

    #region T013 — File path overload

    [Fact]
    public async Task ParseAsync_FilePath_ReturnsSameResultAsTextReader()
    {
        var content = """
            The Great Gatsby (F. Scott Fitzgerald)
            - Your Highlight on Location 100-105 | Added on Thursday, January 1, 2026 12:00:00 AM

            In my younger and more vulnerable years.
            ==========
            """;

        var tempFile = Path.GetTempFileName();
        try
        {
            await File.WriteAllTextAsync(tempFile, content);

            var resultFromFile = await ClippingsParser.ParseAsync(tempFile);

            using var reader = new StringReader(content);
            var resultFromReader = await ClippingsParser.ParseAsync(reader);

            Assert.Equal(resultFromReader.TotalEntriesProcessed, resultFromFile.TotalEntriesProcessed);
            Assert.Equal(resultFromReader.Books.Count, resultFromFile.Books.Count);
            Assert.Equal(resultFromReader.Books[0].Title, resultFromFile.Books[0].Title);
            Assert.Equal(resultFromReader.Books[0].Highlights[0].Text, resultFromFile.Books[0].Highlights[0].Text);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    #endregion
}
