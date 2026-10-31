namespace API.Dto;

internal sealed record SleepShiftDto
{
    public TimeSpan WakeShift { get; init; }
    public TimeSpan SleepShift { get; init; }
}