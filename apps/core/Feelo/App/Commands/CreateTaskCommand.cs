using Domain.Entities.Task;

namespace App.Commands;

public sealed record CreateTaskCommand(
    Guid Id,
    Guid UserId,
    string Name,
    string? Description,
    TaskCategory Type,
    DateTime? From,
    TimeSpan Duration,
    TaskPriority Priority,
    DateTime? Deadline);