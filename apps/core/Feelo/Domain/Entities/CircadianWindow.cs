namespace Domain.Entities;

public sealed record CircadianWindow
{
    public TimeSpan Start { get; init; }
    public TimeSpan End { get; init; }
    public ActivityType Activity { get; init; }
}