namespace API.Dto;

internal sealed record SleepShiftDto
{
    public TimeSpan WakeTimeShift { get; init; }
    public TimeSpan SleepTimeShift { get; init; }
}