using Domain.Entities.Task;
using Riok.Mapperly.Abstractions;

namespace API.Dto.Task;

public sealed record TaskDto : CreateTaskDto
{
    public TaskDto(Guid id, Guid ownerId, DateTime created, DateTime updated, DateTime to, string name,
        string? description, TaskCategory type, DateOnly date, DateTime? from, TimeSpan duration, TaskPriority priority,
        DateTime? deadline, bool isCompleted, DateTime? completedAt, int version)
        : base(id, name, description, type, date, from, duration, priority, deadline)
    {
        OwnerId = ownerId;
        Created = created;
        Updated = updated;
        To = to;
        IsCompleted = isCompleted;
        CompletedAt = completedAt;
        Version = version;
    }
    
    public Guid OwnerId { get; init; }
    public DateTime Created { get; init; }
    public DateTime Updated { get; set; }
    public DateTime To { get; set; }
    public bool IsCompleted { get; set; }
    public DateTime? CompletedAt { get; set; }
    public int Version { get; init; }
}
