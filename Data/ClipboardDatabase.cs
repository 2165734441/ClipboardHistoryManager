using ClipboardHistoryManager.Infrastructure;
using ClipboardHistoryManager.Models;
using Microsoft.Data.Sqlite;

namespace ClipboardHistoryManager.Data;

public sealed class ClipboardDatabase : IDisposable
{
    private readonly string _connectionString;

    public ClipboardDatabase(string? databasePath = null)
    {
        var path = databasePath ?? AppPaths.DatabasePath;
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        _connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = path,
            Mode = SqliteOpenMode.ReadWriteCreate,
            Cache = SqliteCacheMode.Shared
        }.ToString();
    }

    public void Initialize()
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            CREATE TABLE IF NOT EXISTS clipboard_entries (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                content TEXT NOT NULL,
                copied_at TEXT NOT NULL,
                is_favorite INTEGER NOT NULL DEFAULT 0
            );

            CREATE INDEX IF NOT EXISTS idx_clipboard_entries_copied_at
            ON clipboard_entries(copied_at DESC);

            CREATE INDEX IF NOT EXISTS idx_clipboard_entries_is_favorite
            ON clipboard_entries(is_favorite);
            """;
        command.ExecuteNonQuery();
    }

    public ClipboardEntry? GetLatest()
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT id, content, copied_at, is_favorite
            FROM clipboard_entries
            ORDER BY copied_at DESC, id DESC
            LIMIT 1;
            """;

        using var reader = command.ExecuteReader();
        return reader.Read() ? ReadEntry(reader) : null;
    }

    public IReadOnlyList<ClipboardEntry> GetRecent(int limit = 500)
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT id, content, copied_at, is_favorite
            FROM clipboard_entries
            ORDER BY copied_at DESC, id DESC
            LIMIT $limit;
            """;
        command.Parameters.AddWithValue("$limit", limit);

        var entries = new List<ClipboardEntry>();
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            entries.Add(ReadEntry(reader));
        }

        return entries;
    }

    public IReadOnlyList<ClipboardEntry> Search(string? searchText, bool favoritesOnly, int limit = 1000)
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        var whereParts = new List<string>();

        if (favoritesOnly)
        {
            whereParts.Add("is_favorite = 1");
        }

        if (!string.IsNullOrWhiteSpace(searchText))
        {
            whereParts.Add("content LIKE $search ESCAPE '\\' COLLATE NOCASE");
            command.Parameters.AddWithValue("$search", $"%{EscapeLike(searchText.Trim())}%");
        }

        var whereClause = whereParts.Count == 0 ? "" : "WHERE " + string.Join(" AND ", whereParts);
        command.CommandText = $"""
            SELECT id, content, copied_at, is_favorite
            FROM clipboard_entries
            {whereClause}
            ORDER BY copied_at DESC, id DESC
            LIMIT $limit;
            """;
        command.Parameters.AddWithValue("$limit", limit);

        var entries = new List<ClipboardEntry>();
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            entries.Add(ReadEntry(reader));
        }

        return entries;
    }

    public ClipboardEntry Insert(string content, DateTimeOffset copiedAt)
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO clipboard_entries(content, copied_at, is_favorite)
            VALUES ($content, $copiedAt, 0)
            RETURNING id, content, copied_at, is_favorite;
            """;
        command.Parameters.AddWithValue("$content", content);
        command.Parameters.AddWithValue("$copiedAt", copiedAt.ToString("O"));

        using var reader = command.ExecuteReader();
        if (!reader.Read())
        {
            throw new InvalidOperationException("Clipboard entry was not saved.");
        }

        return ReadEntry(reader);
    }

    public void UpdateFavorite(long id, bool isFavorite)
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            UPDATE clipboard_entries
            SET is_favorite = $isFavorite
            WHERE id = $id;
            """;
        command.Parameters.AddWithValue("$id", id);
        command.Parameters.AddWithValue("$isFavorite", isFavorite ? 1 : 0);
        command.ExecuteNonQuery();
    }

    public void UpdateCopiedAt(long id, DateTimeOffset copiedAt)
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            UPDATE clipboard_entries
            SET copied_at = $copiedAt
            WHERE id = $id;
            """;
        command.Parameters.AddWithValue("$id", id);
        command.Parameters.AddWithValue("$copiedAt", copiedAt.ToString("O"));
        command.ExecuteNonQuery();
    }

    public void Delete(long id)
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM clipboard_entries WHERE id = $id;";
        command.Parameters.AddWithValue("$id", id);
        command.ExecuteNonQuery();
    }

    public void Clear(bool includeFavorites)
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = includeFavorites
            ? "DELETE FROM clipboard_entries;"
            : "DELETE FROM clipboard_entries WHERE is_favorite = 0;";
        command.ExecuteNonQuery();
    }

    public int Count(bool favoritesOnly = false)
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = favoritesOnly
            ? "SELECT COUNT(*) FROM clipboard_entries WHERE is_favorite = 1;"
            : "SELECT COUNT(*) FROM clipboard_entries;";
        return Convert.ToInt32(command.ExecuteScalar());
    }

    public void TrimOldNonFavoriteEntries(int maxHistoryEntries)
    {
        if (maxHistoryEntries <= 0)
        {
            return;
        }

        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            DELETE FROM clipboard_entries
            WHERE id IN (
                SELECT id
                FROM clipboard_entries
                WHERE is_favorite = 0
                ORDER BY copied_at ASC, id ASC
                LIMIT (
                    SELECT MAX(COUNT(*) - $maxHistoryEntries, 0)
                    FROM clipboard_entries
                )
            );
            """;
        command.Parameters.AddWithValue("$maxHistoryEntries", maxHistoryEntries);
        command.ExecuteNonQuery();
    }

    private SqliteConnection OpenConnection()
    {
        var connection = new SqliteConnection(_connectionString);
        connection.Open();

        using var command = connection.CreateCommand();
        command.CommandText = "PRAGMA journal_mode=WAL; PRAGMA busy_timeout=3000;";
        command.ExecuteNonQuery();

        return connection;
    }

    private static ClipboardEntry ReadEntry(SqliteDataReader reader)
    {
        return new ClipboardEntry(
            reader.GetInt64(0),
            reader.GetString(1),
            DateTimeOffset.Parse(reader.GetString(2)),
            reader.GetInt64(3) == 1);
    }

    private static string EscapeLike(string value)
    {
        return value
            .Replace(@"\", @"\\", StringComparison.Ordinal)
            .Replace("%", @"\%", StringComparison.Ordinal)
            .Replace("_", @"\_", StringComparison.Ordinal);
    }

    public void Dispose()
    {
    }
}
