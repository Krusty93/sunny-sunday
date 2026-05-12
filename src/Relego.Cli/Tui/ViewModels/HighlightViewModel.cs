namespace Relego.Cli.Tui.ViewModels;

public sealed record HighlightViewModel(
    int Id,
    int BookId,
    int AuthorId,
    string Text,
    string BookTitle,
    string AuthorName,
    bool IsExcluded,
    int? Weight);
