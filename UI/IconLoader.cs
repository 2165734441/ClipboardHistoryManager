namespace ClipboardHistoryManager.UI;

public static class IconLoader
{
    public static Icon LoadApplicationIcon()
    {
        var iconPath = Path.Combine(AppContext.BaseDirectory, "Assets", "app.ico");
        if (File.Exists(iconPath))
        {
            return new Icon(iconPath);
        }

        var executablePath = Environment.ProcessPath ?? Application.ExecutablePath;
        if (!string.IsNullOrWhiteSpace(executablePath) && File.Exists(executablePath))
        {
            var associatedIcon = Icon.ExtractAssociatedIcon(executablePath);
            if (associatedIcon is not null)
            {
                return associatedIcon;
            }
        }

        return SystemIcons.Application;
    }
}
