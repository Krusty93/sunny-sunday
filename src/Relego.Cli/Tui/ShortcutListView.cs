using Terminal.Gui.Input;
using Terminal.Gui.Views;

namespace Relego.Cli.Tui;

/// <summary>
/// A <see cref="ListView"/> that routes printable character keys through
/// <see cref="ShortcutKeyPressed"/> before the built-in <see cref="CollectionNavigator"/>
/// can consume them for type-search.
/// </summary>
internal sealed class ShortcutListView : ListView
{
    /// <summary>
    /// Raised when a shortcut key is pressed. Set <see cref="Key.Handled"/> to true
    /// to prevent the key from reaching the <see cref="CollectionNavigator"/>.
    /// </summary>
    public event EventHandler<Key>? ShortcutKeyPressed;

    protected override bool OnKeyDown(Key key)
    {
        if (key.Handled)
        {
            return true;
        }

        var shortcutChar = GetShortcutChar(key);
        if (shortcutChar is null || ShortcutKeyPressed is null)
        {
            return base.OnKeyDown(key);
        }

        ShortcutKeyPressed.Invoke(this, key);

        // Disable the built-in type-to-select behavior when a screen is explicitly
        // using shortcut handling for printable keys.
        key.Handled = true;
        return true;
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
