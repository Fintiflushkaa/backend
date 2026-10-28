namespace API.Dto;

internal record UpdateTaskDto : BaseTaskDto
{
    public UpdateTaskDto(string name, string description, ActivityType type, TimeSpan duration)
        : base(name, description, type, duration)
    {
    }
    public DateTime From { get; set; }
    public DateTime To { get; set; }
}