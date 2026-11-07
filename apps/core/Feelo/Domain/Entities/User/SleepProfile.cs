namespace Domain.Entities.User;

public sealed record SleepSchedule(
    TimeOnly SleepStart,
    TimeOnly WakeTime);

public sealed record SleepProfile(
    SleepSchedule WorkDays,
    SleepSchedule FreeDays,
    TimeSpan DesiredSleepDuration,
    AlarmUsage FreeDayAlarmUsage,
    TimeOnly? RequiredWakeTime);

public enum AlarmUsage
{
    None,
    Sometimes,
    Always
}