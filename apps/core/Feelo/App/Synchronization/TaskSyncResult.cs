namespace App.Synchronization;
using DomainTask = Domain.Entities.Task.Task;

public record TaskSyncResult<TTask>(
    int? Version,
    TaskSyncStatus Status,
    TTask? ConflictedTask = default,
    string? ErrorMessage = null);

public sealed record TaskSyncResult(
    int? Version,
    TaskSyncStatus Status,
    DomainTask? ConflictedTask = null,
    string? ErrorMessage = null)
    : TaskSyncResult<DomainTask>(Version, Status, ConflictedTask, ErrorMessage);
