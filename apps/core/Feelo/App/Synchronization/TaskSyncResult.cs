namespace App.Synchronization;
using DomainTask = Domain.Entities.Task.Task;

public sealed record TaskSyncResult(
    int? Version,
    TaskSyncStatus Status,
    DomainTask? ConflictedTask = null,
    string? ErrorMessage = null);