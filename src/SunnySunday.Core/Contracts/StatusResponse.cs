namespace SunnySunday.Core.Contracts;

/// <summary>
/// Aggregate status of the Sunny Sunday server for the implicit MVP user.
/// </summary>
public sealed class StatusResponse
{
    /// <summary>Gets or sets the total number of highlights stored for the user.</summary>
    public int TotalHighlights { get; set; }

    /// <summary>Gets or sets the total number of books stored for the user.</summary>
    public int TotalBooks { get; set; }

    /// <summary>Gets or sets the total number of distinct authors stored for the user.</summary>
    public int TotalAuthors { get; set; }

    /// <summary>Gets or sets the number of highlights individually excluded from recaps.</summary>
    public int ExcludedHighlights { get; set; }

    /// <summary>Gets or sets the number of books excluded from recaps.</summary>
    public int ExcludedBooks { get; set; }

    /// <summary>Gets or sets the number of authors excluded from recaps.</summary>
    public int ExcludedAuthors { get; set; }

    /// <summary>
    /// Gets or sets the next scheduled recap time in ISO 8601 format,
    /// or <c>null</c> when scheduling is not yet determined.
    /// </summary>
    public string? NextRecap { get; set; }
}
