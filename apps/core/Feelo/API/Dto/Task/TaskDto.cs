using Domain.Entities.Task;
using System.Text.Json.Serialization;

namespace API.Dto.Task;

public sealed record TaskDto
{
    public TaskDto(Guid id, Guid ownerId, DateTime created, DateTime updated, DateTime to, string name,
        string? description, TaskCategory type, DateOnly date, DateTime? from, TimeSpan duration, TaskPriority priority,
        DateTime? deadline, bool isCompleted, DateTime? completedAt, int version)
    {
        Id = id;
        Name = name;
        Description = description;
        Type = type;
        Date = date;
        From = from;
        Duration = duration;
        Priority = priority;
        Deadline = deadline;
        OwnerId = ownerId;
        Created = created;
        Updated = updated;
        To = to;
        IsCompleted = isCompleted;
        CompletedAt = completedAt;
        Version = version;
    }
    
    public Guid Id { get; init; }
    public string Name { get; init; }
    public string? Description { get; init; }
    public TaskCategory Type { get; init; }
    [JsonRequired]
    public DateOnly Date { get; init; }
    public DateTime? From { get; init; }
    public TimeSpan Duration { get; init; }
    public TaskPriority Priority { get; init; }
    public DateTime? Deadline { get; init; }
    public Guid OwnerId { get; init; }
    public DateTime Created { get; init; }
    public DateTime Updated { get; set; }
    public DateTime To { get; set; }
    public bool IsCompleted { get; set; }
    public DateTime? CompletedAt { get; set; }
    public int Version { get; init; }
}
