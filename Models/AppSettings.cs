namespace ClipboardHistoryManager.Models;

public sealed class AppSettings
{
    public const int MinimumHistoryLimit = 100;
    public const int MaximumHistoryLimit = 5000;
    public const int MinimumPopupWidth = 180;
    public const int MaximumPopupWidth = 2400;
    public const int MinimumPopupHeight = 56;
    public const int MaximumPopupHeight = 1400;

    public HotKeyDefinition OpenHistoryHotKey { get; set; } = HotKeyDefinition.Default;
    public List<HotKeyDefinition> QuickSwitchHotKeys { get; set; } = CreateDefaultQuickSwitchHotKeys();
    public HotKeyDefinition PopupLockHotKey { get; set; } = DefaultPopupLockHotKey;
    public bool StartWithWindows { get; set; }
    public bool StartInTray { get; set; }
    public bool AutoHideAfterCopy { get; set; }
    public bool ShowClipboardPopup { get; set; } = true;
    public bool ClipboardPopupTopMost { get; set; } = true;
    public bool ClipboardPopupUnlocked { get; set; }
    public int ClipboardPopupFontSize { get; set; } = 28;
    public string ClipboardPopupTextColor { get; set; } = "#F4F1E8";
    public int ClipboardPopupX { get; set; } = -1;
    public int ClipboardPopupY { get; set; } = -1;
    public int ClipboardPopupWidth { get; set; } = 520;
    public int ClipboardPopupHeight { get; set; } = 160;
    public int MaxHistoryEntries { get; set; } = MaximumHistoryLimit;

    public void Normalize()
    {
        if (OpenHistoryHotKey.Key == Keys.None)
        {
            OpenHistoryHotKey = HotKeyDefinition.Default;
        }

        if (PopupLockHotKey.Key == Keys.None)
        {
            PopupLockHotKey = DefaultPopupLockHotKey;
        }

        if (QuickSwitchHotKeys is null || QuickSwitchHotKeys.Count != 9 || QuickSwitchHotKeys.Any(hotKey => hotKey.Key == Keys.None))
        {
            QuickSwitchHotKeys = CreateDefaultQuickSwitchHotKeys();
        }

        var allHotKeys = QuickSwitchHotKeys
            .Prepend(OpenHistoryHotKey)
            .Append(PopupLockHotKey)
            .ToList();
        if (allHotKeys.Count != allHotKeys.Distinct().Count())
        {
            OpenHistoryHotKey = HotKeyDefinition.Default;
            QuickSwitchHotKeys = CreateDefaultQuickSwitchHotKeys();
            PopupLockHotKey = DefaultPopupLockHotKey;
        }

        MaxHistoryEntries = Math.Clamp(MaxHistoryEntries, MinimumHistoryLimit, MaximumHistoryLimit);
        ClipboardPopupWidth = Math.Clamp(ClipboardPopupWidth, MinimumPopupWidth, MaximumPopupWidth);
        ClipboardPopupHeight = Math.Clamp(ClipboardPopupHeight, MinimumPopupHeight, MaximumPopupHeight);
        ClipboardPopupFontSize = Math.Clamp(ClipboardPopupFontSize, 12, 96);
        if (!ClipboardPopupTextColor.StartsWith("#", StringComparison.Ordinal) || ClipboardPopupTextColor.Length != 7)
        {
            ClipboardPopupTextColor = "#F4F1E8";
        }
    }

    public static HotKeyDefinition DefaultPopupLockHotKey => new(HotKeyModifiers.Control, Keys.D0);

    public static List<HotKeyDefinition> CreateDefaultQuickSwitchHotKeys()
    {
        return Enumerable.Range(1, 9)
            .Select(number => new HotKeyDefinition(HotKeyModifiers.Control, (Keys)((int)Keys.D0 + number)))
            .ToList();
    }
}
