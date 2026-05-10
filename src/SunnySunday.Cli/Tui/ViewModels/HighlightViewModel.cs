namespace SunnySunday.Cli.Tui.ViewModels;

public sealed record HighlightViewModel(
    int Id,
    string Text,
    string BookTitle,
    string AuthorName,
    bool IsExcluded,
    int? Weight);
