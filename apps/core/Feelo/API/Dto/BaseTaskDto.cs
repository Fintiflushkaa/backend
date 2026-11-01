using Domain.Entities;

namespace API.Dto;

internal record BaseTaskDto
{
    public required string Name { get; set; }
    public required string Description { get; set; }
    public ActivityType Type { get; set; }
    public TimeSpan Duration { get; set; }
}