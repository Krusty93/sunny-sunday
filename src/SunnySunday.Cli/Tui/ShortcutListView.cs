using Terminal.Gui.Input;
using Terminal.Gui.Views;

namespace SunnySunday.Cli.Tui;

/// <summary>
/// A <see cref="ListView"/> that intercepts single-letter shortcut keys (Q, S, R, /)
/// before the built-in <see cref="CollectionNavigator"/> can consume them for type-search.
/// </summary>
internal sealed class ShortcutListView : ListView
{
    private static readonly HashSet<char> ShortcutChars = ['q', 's', 'r', '/'];

    /// <summary>
    /// Raised when a shortcut key is pressed. Set <see cref="Key.Handled"/> to true
    /// to prevent the key from reaching the <see cref="CollectionNavigator"/>.
    /// </summary>
    public event EventHandler<Key>? ShortcutKeyPressed;

    protected override bool OnKeyDown(Key key)
    {
        if (!key.Handled)
        {
            var ch = GetShortcutChar(key);

            if (ch is not null && ShortcutChars.Contains(ch.Value))
            {
                ShortcutKeyPressed?.Invoke(this, key);

                if (key.Handled)
                {
                    return true;
                }
            }
        }

        return base.OnKeyDown(key);
    }

    private static char? GetShortcutChar(Key key)
    {
        var rune = key.AsRune.Value;

        return rune switch
        {
            >= 'a' and <= 'z' => (char)rune,
            >= 'A' and <= 'Z' => char.ToLowerInvariant((char)rune),
            '/' => '/',
            _ => null
        };
    }
}
