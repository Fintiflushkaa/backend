using Domain.Entities.Task;
using Riok.Mapperly.Abstractions;

namespace API.Dto.Task;

public sealed record TaskDto : BaseTaskDto
{
    [MapperConstructor]
    public TaskDto(Guid ownerId, Guid id, DateTime created, DateTime updated, DateTime to,
        string name, string description, TaskCategory type, DateTime from, TimeSpan duration)
        : base(name, description, type, from, duration)
    {
        OwnerId = ownerId;
        Id = id;
        Created = created;
        Updated = updated;
        To = to;
    }
    
    public TaskDto(Guid ownerId, string Name, string Description, TaskCategory Type, DateTime From, TimeSpan Duration)
        : base(Name, Description, Type, From, Duration)
    {
        OwnerId = ownerId;
        Id = Guid.NewGuid();
    }
    
    public Guid OwnerId { get; init; }
    public Guid Id { get; }
    public DateTime Created { get; init; }
    public DateTime Updated { get; set; }
    public DateTime To { get; set; }
}