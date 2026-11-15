namespace API.Dto.User;

public record PlanningSettingsDto(
    TimeSpan BaseDailyCapacity,
    TimeOnly? RequiredWakeTime);