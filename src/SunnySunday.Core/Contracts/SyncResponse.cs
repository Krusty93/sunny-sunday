namespace SunnySunday.Core.Contracts;

/// <summary>
/// Summarizes the outcome of a bulk sync import.
/// </summary>
public sealed record SyncResponse
{
    /// <summary>
    /// Number of highlights imported as new records.
    /// </summary>
    public int NewHighlights { get; set; }

    /// <summary>
    /// Number of highlights skipped because they already existed.
    /// </summary>
    public int DuplicateHighlights { get; set; }

    /// <summary>
    /// Number of new book records created during sync.
    /// </summary>
    public int NewBooks { get; set; }

    /// <summary>
    /// Number of new author records created during sync.
    /// </summary>
    public int NewAuthors { get; set; }
}
