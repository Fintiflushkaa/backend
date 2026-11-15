using Domain.Entities.Task;

namespace API.Dto.Task;

public record BaseTaskDto(
    Guid Id,
    string Name,
    string? Description,
    TaskCategory Type,
    DateTime? From,
    TimeSpan Duration,
    TaskPriority Priority,
    DateTime? Deadline
);