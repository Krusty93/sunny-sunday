namespace SunnySunday.Core.Contracts;

/// <summary>
/// Represents a bulk import request containing parsed Kindle books and highlights.
/// </summary>
public sealed class SyncRequest
{
    /// <summary>
    /// Gets or sets the collection of parsed books to import.
    /// </summary>
    public List<SyncBookRequest> Books { get; set; } = [];
}

/// <summary>
/// Represents a parsed book and its highlights in a bulk sync request.
/// </summary>
public sealed class SyncBookRequest
{
    /// <summary>
    /// Gets or sets the book title.
    /// </summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the author name.
    /// </summary>
    public string? Author { get; set; }

    /// <summary>
    /// Gets or sets the highlights parsed for the book.
    /// </summary>
    public List<SyncHighlightRequest> Highlights { get; set; } = [];
}

/// <summary>
/// Represents a single parsed Kindle highlight in a bulk sync request.
/// </summary>
public sealed class SyncHighlightRequest
{
    /// <summary>
    /// Gets or sets the highlight text.
    /// </summary>
    public string Text { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the original clipping timestamp, when available.
    /// </summary>
    public DateTimeOffset? AddedOn { get; set; }
}
