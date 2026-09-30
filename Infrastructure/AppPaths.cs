namespace ClipboardHistoryManager.Infrastructure;

public static class AppPaths
{
    public static string AppDataDirectory
    {
        get
        {
            var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            return Path.Combine(localAppData, "ClipboardHistoryManager");
        }
    }

    public static string DatabasePath => Path.Combine(AppDataDirectory, "clipboard-history.db");

    public static string SettingsPath => Path.Combine(AppDataDirectory, "settings.json");

    public static string LogPath => Path.Combine(AppDataDirectory, "app.log");
}
