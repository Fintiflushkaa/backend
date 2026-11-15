using Domain.Entities.Task;
using Riok.Mapperly.Abstractions;

namespace API.Dto.Task;

public sealed record TaskDto : BaseTaskDto
{
    public TaskDto(Guid id, Guid ownerId, DateTime created, DateTime updated, DateTime to, string name,
        string? description, TaskCategory type, DateTime? from, TimeSpan duration, TaskPriority priority,
        DateTime? deadline, int version)
        : base(id, name, description, type, from, duration, priority, deadline)
    {
        OwnerId = ownerId;
        Created = created;
        Updated = updated;
        To = to;
        Version = version;
    }
    
    public Guid OwnerId { get; init; }
    public DateTime Created { get; init; }
    public DateTime Updated { get; set; }
    public DateTime To { get; set; }
    public int Version { get; init; }
}