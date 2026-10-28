namespace API.Dto;

internal record BaseTaskDto
{
    public BaseTaskDto(string name, string description, ActivityType type, TimeSpan duration)
    {
        Name = name;
        Description = description;
        Type = type;
        Duration = duration;
    }
    
    public string Name { get; set; }
    public string Description { get; set; }
    public ActivityType Type { get; set; }
    public TimeSpan Duration { get; set; }
}