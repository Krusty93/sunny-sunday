namespace SunnySunday.Core.Contracts;

public sealed class SyncRequest
{
    public List<SyncBookRequest> Books { get; set; } = [];
}

public sealed class SyncBookRequest
{
    public string Title { get; set; } = string.Empty;
    public string? Author { get; set; }
    public List<SyncHighlightRequest> Highlights { get; set; } = [];
}

public sealed class SyncHighlightRequest
{
    public string Text { get; set; } = string.Empty;
    public DateTimeOffset? AddedOn { get; set; }
}
