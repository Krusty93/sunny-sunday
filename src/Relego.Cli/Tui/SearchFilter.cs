using Relego.Cli.Tui.ViewModels;

namespace Relego.Cli.Tui;

public static class SearchFilter
{
    public static List<BookViewModel> Apply(IEnumerable<BookViewModel> books, string query)
    {
        ArgumentNullException.ThrowIfNull(books);

        if (string.IsNullOrWhiteSpace(query))
        {
            return books.ToList();
        }

        var normalizedQuery = query.Trim();

        return books
            .Where(book =>
                book.Title.Contains(normalizedQuery, StringComparison.OrdinalIgnoreCase)
                || book.Author.Contains(normalizedQuery, StringComparison.OrdinalIgnoreCase)
                || book.Highlights.Any(highlight => highlight.Text.Contains(normalizedQuery, StringComparison.OrdinalIgnoreCase)))
            .ToList();
    }
}
