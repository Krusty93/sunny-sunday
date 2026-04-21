namespace SunnySunday.Core.Contracts;

/// <summary>
/// Represents a partial update for the implicit user's recap settings.
/// Only provided properties are changed.
/// </summary>
public sealed record UpdateSettingsRequest
{
    /// <summary>
    /// Recap cadence to apply.
    /// Allowed values: <c>daily</c> or <c>weekly</c>.
    /// Omit to keep the current value.
    /// </summary>
    public string? Schedule { get; set; }

    /// <summary>
    /// Delivery day to use when <c>schedule</c> is <c>weekly</c>.
    /// Omit to keep the current value.
    /// </summary>
    public string? DeliveryDay { get; set; }

    /// <summary>
    /// Delivery time in <c>HH:mm</c> format.
    /// Omit to keep the current value.
    /// </summary>
    public string? DeliveryTime { get; set; }

    /// <summary>
    /// Number of highlights to include in each recap.
    /// Omit to keep the current value.
    /// </summary>
    public int? Count { get; set; }

    /// <summary>
    /// Send-to-Kindle email address for recap delivery.
    /// Omit to keep the current value.
    /// </summary>
    public string? KindleEmail { get; set; }
}
