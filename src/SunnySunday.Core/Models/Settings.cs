namespace SunnySunday.Core.Models;

public class Settings
{
    public int UserId { get; set; }

    public string Schedule { get; set; } = "daily";

    public string? DeliveryDay { get; set; }

    public string DeliveryTime { get; set; } = "18:00";

    public int Count { get; set; } = 3;
}
