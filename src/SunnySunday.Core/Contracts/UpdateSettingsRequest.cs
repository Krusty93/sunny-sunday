namespace SunnySunday.Core.Contracts;

/// <summary>
/// Represents a partial update for the implicit user's recap settings.
/// Only provided properties are changed.
/// </summary>
public sealed class UpdateSettingsRequest
{
    /// <summary>
    /// Gets or sets the recap cadence.
    /// Allowed values are <c>daily</c> and <c>weekly</c>.
    /// </summary>
    public string? Schedule { get; set; }

    /// <summary>
    /// Gets or sets the delivery day used when the schedule is weekly.
    /// </summary>
    public string? DeliveryDay { get; set; }

    /// <summary>
    /// Gets or sets the delivery time in <c>HH:mm</c> format.
    /// </summary>
    public string? DeliveryTime { get; set; }

    /// <summary>
    /// Gets or sets the number of highlights to include in each recap.
    /// </summary>
    public int? Count { get; set; }

    /// <summary>
    /// Gets or sets the Send-to-Kindle email address used for recap delivery.
    /// </summary>
    public string? KindleEmail { get; set; }
}
