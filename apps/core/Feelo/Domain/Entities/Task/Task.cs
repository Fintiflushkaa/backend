using System.ComponentModel.DataAnnotations.Schema;

namespace Domain.Entities.Task;

[Table("Tasks")]
public class Task
{
    public Task(Guid id, Guid ownerId, string name, TaskCategory type, TimeSpan duration, DateTime from,
        TaskPriority priority, DateOnly date, DateTime? deadline = null, string? description = null)
    {
        Id = id;
        OwnerId = ownerId;
        Name = name;
        Description = description;
        Type = type;
        Duration = duration;
        Created = DateTime.UtcNow;
        From = from;
        Date = date;
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
    
    public bool IsCompleted { get; private set; }
    public DateTime? CompletedAt { get; private set; }
    public TimeSpan Duration { get; private set; }
    public DateTime Created { get; init; }
    public DateTime Updated { get; private set; }
    public DateTime From { get; private set; }
    public DateOnly Date { get; private set; }
    public DateTime To { get; private set; }
    public TaskPriority Priority { get; private set; }
    public DateTime? Deadline { get; private set; }
    
    public int Version { get; private set; }

    public void UpdateTask(string? name, TaskCategory? type, TimeSpan? duration, DateTime? from, TaskPriority? priority,
        DateTime? deadline, bool? isCompleted, DateTime? completedAt,
        string? description = null, bool descriptionSpecified = false, DateOnly? date = null)
    {
        if (date.HasValue && date.Value != Date && from is null)
            throw new ArgumentException("Для переноса задачи на другую дату нужно определить время начала", nameof(from));

        Name = name ?? Name;
        if (descriptionSpecified || description is not null)
            Description = description;
        Type = type ?? Type;
        Duration = duration ?? Duration;
        From = from ?? From;
        Date = date ?? Date;
        To = From + Duration;
        Updated = DateTime.UtcNow;
        Priority = priority ?? Priority;
        Deadline = deadline ?? Deadline;
        UpdateCompletionFields(isCompleted, completedAt);
        ++Version;
    }

    private void UpdateCompletionFields(bool? isCompleted, DateTime? completedAt)
    {
        if (IsCompleted && isCompleted.HasValue && !isCompleted.Value)
        {
            IsCompleted = false;
            CompletedAt = null;
            return;
        }
        if (!IsCompleted && isCompleted.HasValue && isCompleted.Value && !completedAt.HasValue)
        {
            CompletedAt = DateTime.UtcNow;
            IsCompleted = true;
            return;
        }

        if (isCompleted.HasValue && !isCompleted.Value)
        {
            IsCompleted = false;
            CompletedAt = null;
            return;
        }

        IsCompleted = isCompleted ?? IsCompleted;
        CompletedAt = completedAt ?? CompletedAt;
    }
}
