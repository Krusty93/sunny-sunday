namespace SunnySunday.Core.Contracts;

/// <summary>
/// Updates the recap weight for a specific highlight.
/// </summary>
public sealed record SetWeightRequest
{
    /// <summary>
    /// New weight to apply to the highlight.
    /// Allowed values: 1 through 5.
    /// </summary>
    public int Weight { get; set; }
}
