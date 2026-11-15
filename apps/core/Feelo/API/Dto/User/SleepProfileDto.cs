namespace API.Dto.User;

public sealed record SleepProfileDto(
    SleepScheduleDto WorkDays,
    SleepScheduleDto FreeDays,
    TimeSpan DesiredSleepDuration,
    AlarmUsage FreeDayAlarmUsage,
    TimeOnly? RequiredWakeTime);