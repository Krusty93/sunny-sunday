namespace SunnySunday.Core.Contracts;

public sealed class SyncResponse
{
    public int NewHighlights { get; set; }
    public int DuplicateHighlights { get; set; }
    public int NewBooks { get; set; }
    public int NewAuthors { get; set; }
}
