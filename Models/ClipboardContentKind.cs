namespace ClipboardHistoryManager.Models;

public enum ClipboardContentKind
{
    Text,
    Image,
    Unknown
}

public sealed class ClipboardContentChangedEventArgs : EventArgs
{
    public ClipboardContentChangedEventArgs(ClipboardContentKind kind, string displayText, string? text)
    {
        Kind = kind;
        DisplayText = displayText;
        Text = text;
    }

    public ClipboardContentKind Kind { get; }

    public string DisplayText { get; }

    public string? Text { get; }
}
