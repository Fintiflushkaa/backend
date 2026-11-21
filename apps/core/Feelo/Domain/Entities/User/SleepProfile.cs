namespace Domain.Entities.User;

public sealed record SleepSchedule(
    TimeOnly SleepStart,
    TimeOnly WakeTime);

public sealed record SleepProfile(
    SleepSchedule WorkDays,
    SleepSchedule FreeDays,
    TimeSpan DesiredSleepDuration,
    AlarmUsage FreeDayAlarmUsage,
    TimeOnly? RequiredWakeTime)
{
    // Clock times from the profile. The caller supplies the time zone.
    public (DateTime Start, DateTime End) GetAwakeWindow(DateOnly date)
    {
        if (DesiredSleepDuration <= TimeSpan.Zero)
            throw new ArgumentException("Желаемая длительность сна должна быть больше нуля");

        var tomorrow = date.AddDays(1);
        var start = date.ToDateTime(GetWakeTime(date));
        var preferredBedtime = date.ToDateTime(GetSchedule(tomorrow).SleepStart);
        if (preferredBedtime <= start)
            preferredBedtime = preferredBedtime.AddDays(1);

        var nextWake = tomorrow.ToDateTime(GetWakeTime(tomorrow));
        var latestBedtime = nextWake - DesiredSleepDuration;
        var end = preferredBedtime < latestBedtime ? preferredBedtime : latestBedtime;
        if (end <= start)
            throw new ArgumentException("Режим сна не оставляет времени для задач до следующего подъёма");

        return (start, end);
    }

    private SleepSchedule GetSchedule(DateOnly date) =>
        IsWorkDay(date) ? WorkDays : FreeDays;

    private TimeOnly GetWakeTime(DateOnly date)
    {
        var wake = GetSchedule(date).WakeTime;
        return IsWorkDay(date) && RequiredWakeTime is { } required && required < wake
            ? required : wake;
    }

    private static bool IsWorkDay(DateOnly date) =>
        date.DayOfWeek is not (DayOfWeek.Saturday or DayOfWeek.Sunday);
}

public enum AlarmUsage
{
    None,
    Sometimes,
    Always
}
