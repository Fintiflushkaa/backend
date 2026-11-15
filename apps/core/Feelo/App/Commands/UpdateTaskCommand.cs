using Domain.Entities.Task;

namespace App.Commands;

public sealed record UpdateTaskCommand(
    Guid UserId,
    Guid TaskId,
    string? Name = null,
    TaskCategory? Type = null,
    TimeSpan? Duration = null,
    DateTime? From = null,
    TaskPriority? Priority = null,
    DateTime? Deadline = null,
    string? Description = null,
    bool DescriptionSpecified = false
);
