using Domain.Entities.Task;
using System.Text.Json.Serialization;

namespace API.Dto.Task;

public record CreateTaskDto(
    Guid Id,
    string Name,
    string? Description,
    TaskCategory Type,
    [property: JsonRequired] DateOnly Date,
    DateTime? From,
    TimeSpan Duration,
    TaskPriority Priority,
    DateTime? Deadline
);
