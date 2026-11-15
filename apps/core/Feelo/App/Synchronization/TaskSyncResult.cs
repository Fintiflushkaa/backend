namespace App.Synchronization;

public sealed record TaskSyncResult(
    TaskSyncStatus Status,
    string? ErrorMessage = null);