using Relego.Core.Contracts;

namespace Relego.Cli.Tui.ViewModels;

public sealed record BookViewModel(
    int BookId,
    int AuthorId,
    string Title,
    string Author,
    int HighlightCount,
    bool IsBookExcluded,
    bool IsAuthorExcluded,
    IReadOnlyList<HighlightViewModel> Highlights)
{
    public static List<BookViewModel> FromHighlights(IEnumerable<HighlightItemDto> items)
    {
        ArgumentNullException.ThrowIfNull(items);

        return items
            .GroupBy(item => (item.BookId, item.AuthorId, item.BookTitle, item.AuthorName))
            .Select(group =>
            {
                var highlights = group
                    .Select(item => new HighlightViewModel(
                        item.Id,
                        item.BookId,
                        item.AuthorId,
                        item.Text,
                        item.BookTitle,
                        item.AuthorName,
                        false,
                        null))
                    .ToList();

                return new BookViewModel(
                    group.Key.BookId,
                    group.Key.AuthorId,
                    group.Key.BookTitle,
                    group.Key.AuthorName,
                    highlights.Count,
                    false,
                    false,
                    highlights);
            })
            .OrderBy(book => book.Title, StringComparer.OrdinalIgnoreCase)
            .ThenBy(book => book.Author, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }
}
