using Domain.Entities.Task;

namespace API.Dto.Task;

public record BaseTaskDto(
    string Name,
    string Description,
    TaskCategory Type,
    DateTime From,
    TimeSpan Duration
);