namespace SunnySunday.Server.Models;

public class User
{
    public int Id { get; set; }

    public string KindleEmail { get; set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; set; }
}
