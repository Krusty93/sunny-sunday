namespace SunnySunday.Core.Contracts;

/// <summary>
/// Paginated list of highlights.
/// </summary>
public sealed record HighlightsResponse
{
    /// <summary>
    /// Total number of highlights matching the current filter.
    /// </summary>
    public int Total { get; set; }

    /// <summary>
    /// Current page number (1-based).
    /// </summary>
    public int Page { get; set; }

    /// <summary>
    /// Number of items per page.
    /// </summary>
    public int PageSize { get; set; }

    /// <summary>
    /// Highlights on the current page.
    /// </summary>
    public List<HighlightItemDto> Items { get; set; } = [];
}

/// <summary>
/// A single highlight entry in a paginated list.
/// </summary>
public sealed record HighlightItemDto
{
    /// <summary>
    /// Highlight identifier.
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// Full highlight text.
    /// </summary>
    public string Text { get; set; } = string.Empty;

    /// <summary>
    /// Title of the book containing the highlight.
    /// </summary>
    public string BookTitle { get; set; } = string.Empty;

    /// <summary>
    /// Name of the author of the book.
    /// </summary>
    public string AuthorName { get; set; } = string.Empty;
}
