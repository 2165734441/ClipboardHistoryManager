using ClipboardHistoryManager.Data;
using ClipboardHistoryManager.Infrastructure;
using ClipboardHistoryManager.Models;

namespace ClipboardHistoryManager.Services;

public sealed class ClipboardHistoryService : IDisposable
{
    private readonly ClipboardDatabase _database;
    private readonly object _gate = new();
    private string? _latestContent;
    private string? _suppressedClipboardContent;
    private DateTimeOffset _suppressedUntil;
    private bool _isRecordingPaused;
    private int _maxHistoryEntries = AppSettings.MaximumHistoryLimit;

    public ClipboardHistoryService(ClipboardDatabase database)
    {
        _database = database;
        _latestContent = database.GetLatest()?.Content;
    }

    public event EventHandler<ClipboardEntry>? EntryAdded;
    public event EventHandler? HistoryChanged;
    public event EventHandler? RecordingStateChanged;

    public bool IsRecordingPaused
    {
        get
        {
            lock (_gate)
            {
                return _isRecordingPaused;
            }
        }
    }

    public IReadOnlyList<ClipboardEntry> GetRecent(int limit = 500)
    {
        lock (_gate)
        {
            return _database.GetRecent(limit);
        }
    }

    public IReadOnlyList<ClipboardEntry> Search(string? searchText, bool favoritesOnly, int limit = 1000)
    {
        lock (_gate)
        {
            return _database.Search(searchText, favoritesOnly, limit);
        }
    }

    public ClipboardEntry? SaveCopiedText(string? content)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return null;
        }

        lock (_gate)
        {
            if (_isRecordingPaused)
            {
                return null;
            }

            if (ShouldSuppress(content))
            {
                _latestContent = content;
                return null;
            }

            if (string.Equals(_latestContent, content, StringComparison.Ordinal))
            {
                return null;
            }

            var entry = _database.Insert(content, DateTimeOffset.Now);
            TryTrimHistory();
            _latestContent = content;
            EntryAdded?.Invoke(this, entry);
            HistoryChanged?.Invoke(this, EventArgs.Empty);
            return entry;
        }
    }

    public void SetMaxHistoryEntries(int maxHistoryEntries)
    {
        lock (_gate)
        {
            _maxHistoryEntries = Math.Clamp(
                maxHistoryEntries,
                AppSettings.MinimumHistoryLimit,
                AppSettings.MaximumHistoryLimit);
            TryTrimHistory();
            _latestContent = _database.GetLatest()?.Content;
            HistoryChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    public void SetRecordingPaused(bool paused)
    {
        lock (_gate)
        {
            if (_isRecordingPaused == paused)
            {
                return;
            }

            _isRecordingPaused = paused;
            RecordingStateChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    public void MarkClipboardWriteFromHistory(string content)
    {
        lock (_gate)
        {
            _suppressedClipboardContent = content;
            _suppressedUntil = DateTimeOffset.Now.AddSeconds(2);
            _latestContent = content;
        }
    }

    public void SetFavorite(long id, bool isFavorite)
    {
        lock (_gate)
        {
            _database.UpdateFavorite(id, isFavorite);
            HistoryChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    public void Delete(long id)
    {
        lock (_gate)
        {
            _database.Delete(id);
            _latestContent = _database.GetLatest()?.Content;
            HistoryChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    public void Clear(bool includeFavorites)
    {
        lock (_gate)
        {
            _database.Clear(includeFavorites);
            _latestContent = _database.GetLatest()?.Content;
            HistoryChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    private bool ShouldSuppress(string content)
    {
        if (_suppressedClipboardContent is null || DateTimeOffset.Now > _suppressedUntil)
        {
            _suppressedClipboardContent = null;
            return false;
        }

        if (!string.Equals(_suppressedClipboardContent, content, StringComparison.Ordinal))
        {
            return false;
        }

        _suppressedClipboardContent = null;
        return true;
    }

    private void TryTrimHistory()
    {
        try
        {
            _database.TrimOldNonFavoriteEntries(_maxHistoryEntries);
        }
        catch (Exception ex)
        {
            AppLogger.Error("History trimming failed.", ex);
        }
    }

    public void Dispose()
    {
    }
}
