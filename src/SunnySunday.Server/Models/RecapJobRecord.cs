namespace SunnySunday.Server.Models;

public class RecapJobRecord
{
    public int Id { get; set; }

    public int UserId { get; set; }

    public DateTimeOffset ScheduledFor { get; set; }

    public string Status { get; set; } = "pending";

    public int AttemptCount { get; set; }

    public string? ErrorMessage { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset? DeliveredAt { get; set; }
}
