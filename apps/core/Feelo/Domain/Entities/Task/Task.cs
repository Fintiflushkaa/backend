using System.ComponentModel.DataAnnotations.Schema;

namespace Domain.Entities.Task;

[Table("Tasks")]
public class Task
{
    public Task(Guid id, Guid ownerId, string name, TaskCategory type, TimeSpan duration, DateTime from,
        TaskPriority priority, DateTime? deadline = null, string? description = null)
    {
        Id = id;
        OwnerId = ownerId;
        Name = name;
        Description = description;
        Type = type;
        Duration = duration;
        Created = DateTime.UtcNow;
        From = from;
        To = from + duration;
        Priority = priority;
        Deadline = deadline;
        Version = 0;
    }
    
    public Guid Id { get; }
    public Guid OwnerId { get; init; }
    
    public string Name { get; private set; }
    public string? Description { get; private set; }
    public TaskCategory Type { get; private set; }
    
    public TimeSpan Duration { get; private set; }
    public DateTime Created { get; init; }
    public DateTime Updated { get; private set; }
    public DateTime From { get; private set; }
    public DateTime To { get; private set; }
    public TaskPriority Priority { get; private set; }
    public DateTime? Deadline { get; private set; }
    
    public int Version { get; private set; }

    public void UpdateTask(string? name, TaskCategory? type, TimeSpan? duration, DateTime? from, TaskPriority? priority,
        DateTime? deadline, string? description = null, bool descriptionSpecified = false)
    {
        Name = name ?? Name;
        if (descriptionSpecified || description is not null)
            Description = description;
        Type = type ?? Type;
        Duration = duration ?? Duration;
        From = from ?? From;
        if (from is null && duration is not null)
            To = From + duration.Value;
        else if (from is not null && duration is null)
            To = from.Value + Duration;
        else if (from is not null && duration is not null)
            To = from.Value + duration.Value;
        Updated = DateTime.UtcNow;
        Priority = priority ?? Priority;
        Deadline = deadline ?? Deadline;
        ++Version;
    }
}
