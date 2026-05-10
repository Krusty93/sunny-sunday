using SunnySunday.Core.Contracts;

namespace SunnySunday.Cli.Tui.ViewModels;

public sealed record BookViewModel(
    string Title,
    string Author,
    int HighlightCount,
    IReadOnlyList<HighlightViewModel> Highlights)
{
    public static List<BookViewModel> FromHighlights(IEnumerable<HighlightItemDto> items)
    {
        ArgumentNullException.ThrowIfNull(items);

        return items
            .GroupBy(item => (item.BookTitle, item.AuthorName))
            .Select(group =>
            {
                var highlights = group
                    .Select(item => new HighlightViewModel(
                        item.Id,
                        item.Text,
                        item.BookTitle,
                        item.AuthorName,
                        false,
                        null))
                    .ToList();

                return new BookViewModel(
                    group.Key.BookTitle,
                    group.Key.AuthorName,
                    highlights.Count,
                    highlights);
            })
            .OrderBy(book => book.Title, StringComparer.OrdinalIgnoreCase)
            .ThenBy(book => book.Author, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }
}
