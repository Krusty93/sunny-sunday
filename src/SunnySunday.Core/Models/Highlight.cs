namespace SunnySunday.Core.Models;

public class Highlight
{
    public int Id { get; set; }

    public int UserId { get; set; }

    public int BookId { get; set; }

    public string Text { get; set; } = string.Empty;

    public int Weight { get; set; } = 3;

    public bool Excluded { get; set; }

    public DateTimeOffset? LastSeen { get; set; }

    public int DeliveryCount { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
}
