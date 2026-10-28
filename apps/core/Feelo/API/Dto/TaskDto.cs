namespace API.Dto;

internal sealed record TaskDto : BaseTaskDto
{
    public TaskDto(string name, string description, ActivityType type, TimeSpan duration, Guid ownerId)
        : base(name, description, type, duration)
    {
        OwnerId = ownerId;
        Id = Guid.NewGuid();
    }
    
    public Guid OwnerId { get; init; }
    public Guid Id { get; }
    public DateTime Created { get; init; }
    public DateTime Updated { get; set; }
    public DateTime From { get; set; }
    public DateTime To { get; set; }
}