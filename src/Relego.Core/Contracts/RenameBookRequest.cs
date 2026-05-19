namespace Relego.Core.Contracts;

/// <summary>
/// Request body for renaming a book.
/// </summary>
public sealed record RenameBookRequest
{
    /// <summary>
    /// New title for the book. Must be non-empty.
    /// </summary>
    public string Title { get; set; } = string.Empty;
}
