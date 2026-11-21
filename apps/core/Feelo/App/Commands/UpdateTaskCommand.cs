using Domain.Entities.Task;

namespace App.Commands;

public sealed record UpdateTaskCommand(
    Guid UserId,
    Guid TaskId,
    int Version,
    string? Name = null,
    TaskCategory? Type = null,
    TimeSpan? Duration = null,
    DateTime? From = null,
    TaskPriority? Priority = null,
    DateTime? Deadline = null,
    bool? IsCompleted = null,
    DateTime? CompletedAt = null,
    string? Description = null,
    bool DescriptionSpecified = false,
    DateOnly? Date = null
);
