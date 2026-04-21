namespace SunnySunday.Core.Contracts;

/// <summary>
/// Represents a bulk import request containing parsed Kindle books and highlights.
/// </summary>
public sealed record SyncRequest
{
    /// <summary>
    /// Books to import in the current sync operation.
    /// </summary>
    public List<SyncBookRequest> Books { get; set; } = [];
}

/// <summary>
/// Represents a parsed book and its highlights in a bulk sync request.
/// </summary>
public sealed record SyncBookRequest
{
    /// <summary>
    /// Book title exactly as parsed from the source.
    /// </summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// Author name as parsed from the source, or <c>null</c> when unavailable.
    /// </summary>
    public string? Author { get; set; }

    /// <summary>
    /// Highlights parsed for this book.
    /// </summary>
    public List<SyncHighlightRequest> Highlights { get; set; } = [];
}

/// <summary>
/// Represents a single parsed Kindle highlight in a bulk sync request.
/// </summary>
public sealed record SyncHighlightRequest
{
    /// <summary>
    /// Highlight text content.
    /// </summary>
    public string Text { get; set; } = string.Empty;

    /// <summary>
    /// Original clipping timestamp from the source, when available.
    /// </summary>
    public DateTimeOffset? AddedOn { get; set; }
}
