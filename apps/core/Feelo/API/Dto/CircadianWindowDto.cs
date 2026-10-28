namespace API.Dto;

internal sealed record CircadianWindowDto
{
    public TimeSpan Start { get; init; }
    public TimeSpan End { get; init; }
    public ActivityType Activity { get; init; }
}