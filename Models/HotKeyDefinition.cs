namespace ClipboardHistoryManager.Models;

[Flags]
public enum HotKeyModifiers
{
    None = 0,
    Alt = 1,
    Control = 2,
    Shift = 4,
    Win = 8
}

public sealed record HotKeyDefinition(HotKeyModifiers Modifiers, Keys Key)
{
    public static HotKeyDefinition Default => new(HotKeyModifiers.Control | HotKeyModifiers.Shift, Keys.V);

    public override string ToString()
    {
        var parts = new List<string>();

        if (Modifiers.HasFlag(HotKeyModifiers.Control))
        {
            parts.Add("Ctrl");
        }

        if (Modifiers.HasFlag(HotKeyModifiers.Shift))
        {
            parts.Add("Shift");
        }

        if (Modifiers.HasFlag(HotKeyModifiers.Alt))
        {
            parts.Add("Alt");
        }

        if (Modifiers.HasFlag(HotKeyModifiers.Win))
        {
            parts.Add("Win");
        }

        parts.Add(FormatKey(Key));
        return string.Join(" + ", parts);
    }

    private static string FormatKey(Keys key)
    {
        if (key >= Keys.D0 && key <= Keys.D9)
        {
            return ((int)key - (int)Keys.D0).ToString();
        }

        if (key >= Keys.NumPad0 && key <= Keys.NumPad9)
        {
            return $"小键盘{(int)key - (int)Keys.NumPad0}";
        }

        return key.ToString().ToUpperInvariant();
    }
}
