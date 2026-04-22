namespace SunnySunday.Core.Contracts;

/// <summary>
/// Summary of a highlight that has a non-default recap weight.
/// </summary>
public sealed record WeightedHighlightDto
{
    /// <summary>
    /// Highlight identifier.
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// Highlight text truncated to the first 100 characters.
    /// </summary>
    public string Text { get; set; } = string.Empty;

    /// <summary>
    /// Title of the book containing the highlight.
    /// </summary>
    public string BookTitle { get; set; } = string.Empty;

    /// <summary>
    /// Current highlight weight.
    /// </summary>
    public int Weight { get; set; }
}
