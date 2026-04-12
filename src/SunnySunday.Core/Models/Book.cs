namespace SunnySunday.Core.Models;

public class Book
{
    public int Id { get; set; }

    public int UserId { get; set; }

    public int AuthorId { get; set; }

    public string Title { get; set; } = string.Empty;
}
