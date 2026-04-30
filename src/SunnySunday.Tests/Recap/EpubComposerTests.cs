using System.IO.Compression;
using System.Text;
using SunnySunday.Server.Services;

namespace SunnySunday.Tests.Recap;

public sealed class EpubComposerTests
{
    private static readonly DateTimeOffset RecapDate = new(2026, 4, 20, 18, 0, 0, TimeSpan.Zero);

    private static readonly IReadOnlyList<SelectionCandidate> SampleHighlights =
    [
        new(1, "The only way to do great work is to love what you do.", "Steve Jobs Biography", "Walter Isaacson", 5, null, RecapDate.AddDays(-30), 10),
        new(2, "In the middle of difficulty lies opportunity.", "Collected Works", "Albert Einstein", 3, RecapDate.AddDays(-7), RecapDate.AddDays(-60), 8),
        new(3, "Text with <special> & \"characters\"", "Book & Title", "Author <Name>", 2, null, RecapDate.AddDays(-10), 5),
    ];

    [Fact]
    public void Compose_ReturnsValidZipArchive()
    {
        var epub = EpubComposer.Compose(SampleHighlights, RecapDate);

        using var stream = new MemoryStream(epub);
        using var archive = new ZipArchive(stream, ZipArchiveMode.Read);

        Assert.True(archive.Entries.Count > 0);
    }

    [Fact]
    public void Compose_MimetypeIsFirstEntry()
    {
        var epub = EpubComposer.Compose(SampleHighlights, RecapDate);

        using var stream = new MemoryStream(epub);
        using var archive = new ZipArchive(stream, ZipArchiveMode.Read);

        var firstEntry = archive.Entries[0];
        Assert.Equal("mimetype", firstEntry.FullName);
    }

    [Fact]
    public void Compose_MimetypeIsUncompressed()
    {
        var epub = EpubComposer.Compose(SampleHighlights, RecapDate);

        using var stream = new MemoryStream(epub);
        using var archive = new ZipArchive(stream, ZipArchiveMode.Read);

        var mimetypeEntry = archive.GetEntry("mimetype")!;
        // Uncompressed: CompressedLength == Length
        Assert.Equal(mimetypeEntry.Length, mimetypeEntry.CompressedLength);
    }

    [Fact]
    public void Compose_MimetypeContentIsCorrect()
    {
        var epub = EpubComposer.Compose(SampleHighlights, RecapDate);

        using var stream = new MemoryStream(epub);
        using var archive = new ZipArchive(stream, ZipArchiveMode.Read);

        var content = ReadEntry(archive, "mimetype");
        Assert.Equal("application/epub+zip", content);
    }

    [Fact]
    public void Compose_ContainsRequiredEpubFiles()
    {
        var epub = EpubComposer.Compose(SampleHighlights, RecapDate);

        using var stream = new MemoryStream(epub);
        using var archive = new ZipArchive(stream, ZipArchiveMode.Read);

        Assert.NotNull(archive.GetEntry("META-INF/container.xml"));
        Assert.NotNull(archive.GetEntry("OEBPS/content.opf"));
        Assert.NotNull(archive.GetEntry("OEBPS/toc.ncx"));
        Assert.NotNull(archive.GetEntry("OEBPS/highlights.xhtml"));
    }

    [Fact]
    public void Compose_ContainerXmlPointsToContentOpf()
    {
        var epub = EpubComposer.Compose(SampleHighlights, RecapDate);

        using var stream = new MemoryStream(epub);
        using var archive = new ZipArchive(stream, ZipArchiveMode.Read);

        var containerXml = ReadEntry(archive, "META-INF/container.xml");
        Assert.Contains("OEBPS/content.opf", containerXml);
        Assert.Contains("application/oebps-package+xml", containerXml);
    }

    [Fact]
    public void Compose_ContentOpfContainsRecapDateInTitle()
    {
        var epub = EpubComposer.Compose(SampleHighlights, RecapDate);

        using var stream = new MemoryStream(epub);
        using var archive = new ZipArchive(stream, ZipArchiveMode.Read);

        var opf = ReadEntry(archive, "OEBPS/content.opf");
        Assert.Contains("2026-04-20", opf);
        Assert.Contains("Sunny Sunday Recap", opf);
    }

    [Fact]
    public void Compose_HighlightsXhtml_RendersFlatList()
    {
        var epub = EpubComposer.Compose(SampleHighlights, RecapDate);

        using var stream = new MemoryStream(epub);
        using var archive = new ZipArchive(stream, ZipArchiveMode.Read);

        var xhtml = ReadEntry(archive, "OEBPS/highlights.xhtml");
        Assert.Contains("<ul>", xhtml);
        Assert.Contains("</ul>", xhtml);
        Assert.Contains("<li>", xhtml);
    }

    [Fact]
    public void Compose_HighlightsXhtml_PreservesInputOrder()
    {
        var epub = EpubComposer.Compose(SampleHighlights, RecapDate);

        using var stream = new MemoryStream(epub);
        using var archive = new ZipArchive(stream, ZipArchiveMode.Read);

        var xhtml = ReadEntry(archive, "OEBPS/highlights.xhtml");

        var firstIndex = xhtml.IndexOf("The only way to do great work", StringComparison.Ordinal);
        var secondIndex = xhtml.IndexOf("In the middle of difficulty", StringComparison.Ordinal);
        var thirdIndex = xhtml.IndexOf("special", StringComparison.Ordinal);

        Assert.True(firstIndex < secondIndex, "First highlight should appear before second");
        Assert.True(secondIndex < thirdIndex, "Second highlight should appear before third");
    }

    [Fact]
    public void Compose_HighlightsXhtml_IncludesSourceMetadata()
    {
        var epub = EpubComposer.Compose(SampleHighlights, RecapDate);

        using var stream = new MemoryStream(epub);
        using var archive = new ZipArchive(stream, ZipArchiveMode.Read);

        var xhtml = ReadEntry(archive, "OEBPS/highlights.xhtml");

        // Each highlight should include book title and author
        Assert.Contains("Steve Jobs Biography", xhtml);
        Assert.Contains("Walter Isaacson", xhtml);
        Assert.Contains("Collected Works", xhtml);
        Assert.Contains("Albert Einstein", xhtml);
    }

    [Fact]
    public void Compose_HighlightsXhtml_EscapesSpecialCharacters()
    {
        var epub = EpubComposer.Compose(SampleHighlights, RecapDate);

        using var stream = new MemoryStream(epub);
        using var archive = new ZipArchive(stream, ZipArchiveMode.Read);

        var xhtml = ReadEntry(archive, "OEBPS/highlights.xhtml");

        // Special characters must be escaped
        Assert.Contains("&lt;special&gt;", xhtml);
        Assert.Contains("&amp;", xhtml);
        Assert.Contains("&quot;characters&quot;", xhtml);
        Assert.Contains("Book &amp; Title", xhtml);
        Assert.Contains("Author &lt;Name&gt;", xhtml);
    }

    [Fact]
    public void Compose_EmptyHighlights_ProducesValidEpubWithEmptyList()
    {
        var epub = EpubComposer.Compose([], RecapDate);

        using var stream = new MemoryStream(epub);
        using var archive = new ZipArchive(stream, ZipArchiveMode.Read);

        Assert.NotNull(archive.GetEntry("OEBPS/highlights.xhtml"));
        var xhtml = ReadEntry(archive, "OEBPS/highlights.xhtml");
        Assert.Contains("<ul>", xhtml);
        Assert.Contains("</ul>", xhtml);
        Assert.DoesNotContain("<li>", xhtml);
    }

    private static string ReadEntry(ZipArchive archive, string path)
    {
        var entry = archive.GetEntry(path);
        Assert.NotNull(entry);
        using var reader = new StreamReader(entry!.Open(), Encoding.UTF8);
        return reader.ReadToEnd();
    }
}
