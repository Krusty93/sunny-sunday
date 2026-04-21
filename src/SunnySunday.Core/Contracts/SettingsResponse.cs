namespace SunnySunday.Core.Contracts;

/// <summary>
/// Describes the effective recap settings currently stored for the implicit Sunny Sunday user.
/// </summary>
public sealed class SettingsResponse
{
    /// <summary>
    /// Gets or sets the recap cadence.
    /// Allowed values are <c>daily</c> and <c>weekly</c>.
    /// </summary>
    public string Schedule { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the delivery day when the schedule is weekly.
    /// </summary>
    public string? DeliveryDay { get; set; }

    /// <summary>
    /// Gets or sets the recap delivery time in <c>HH:mm</c> format.
    /// </summary>
    public string DeliveryTime { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the number of highlights included in each recap.
    /// </summary>
    public int Count { get; set; }

    /// <summary>
    /// Gets or sets the Send-to-Kindle email address used for recap delivery.
    /// </summary>
    public string KindleEmail { get; set; } = string.Empty;
}
