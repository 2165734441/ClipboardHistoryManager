namespace ClipboardHistoryManager.Models;

public sealed record ClipboardEntry(
    long Id,
    string Content,
    DateTimeOffset CopiedAt,
    bool IsFavorite);
