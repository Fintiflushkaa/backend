namespace Domain.Entities;

public sealed record SleepShift
{
    public TimeSpan WakeTimeShift { get; init; }
    public TimeSpan SleepTimeShift { get; init; }
}