namespace API.Dto.User;

public sealed record SleepScheduleDto(
    TimeOnly SleepStart,
    TimeOnly WakeTime);