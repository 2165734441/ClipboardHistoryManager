namespace ClipboardHistoryManager.Infrastructure;

public static class AppLogger
{
    private const long MaxLogBytes = 1024 * 1024;
    private static readonly object Gate = new();

    public static void Info(string message)
    {
        Write("INFO", message, null);
    }

    public static void Error(string message, Exception? exception = null)
    {
        Write("ERROR", message, exception);
    }

    private static void Write(string level, string message, Exception? exception)
    {
        lock (Gate)
        {
            try
            {
                Directory.CreateDirectory(AppPaths.AppDataDirectory);
                RotateIfNeeded();

                var line = $"{DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss.fff zzz} [{level}] {message}";
                if (exception is not null)
                {
                    line += Environment.NewLine + exception;
                }

                File.AppendAllText(AppPaths.LogPath, line + Environment.NewLine);
            }
            catch
            {
                // Logging must never break the app.
            }
        }
    }

    private static void RotateIfNeeded()
    {
        if (!File.Exists(AppPaths.LogPath))
        {
            return;
        }

        var fileInfo = new FileInfo(AppPaths.LogPath);
        if (fileInfo.Length < MaxLogBytes)
        {
            return;
        }

        var archivePath = Path.Combine(AppPaths.AppDataDirectory, "app.old.log");
        File.Delete(archivePath);
        File.Move(AppPaths.LogPath, archivePath);
    }
}
