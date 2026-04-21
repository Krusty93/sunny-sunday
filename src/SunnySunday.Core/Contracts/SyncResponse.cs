namespace SunnySunday.Core.Contracts;

/// <summary>
/// Summarizes the outcome of a bulk sync import.
/// </summary>
public sealed class SyncResponse
{
    /// <summary>
    /// Gets or sets the number of newly imported highlights.
    /// </summary>
    public int NewHighlights { get; set; }

    /// <summary>
    /// Gets or sets the number of skipped duplicate highlights.
    /// </summary>
    public int DuplicateHighlights { get; set; }

    /// <summary>
    /// Gets or sets the number of newly created books.
    /// </summary>
    public int NewBooks { get; set; }

    /// <summary>
    /// Gets or sets the number of newly created authors.
    /// </summary>
    public int NewAuthors { get; set; }
}
