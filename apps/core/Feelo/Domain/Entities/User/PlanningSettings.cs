namespace Domain.Entities.User;

public sealed record PlanningSettings(
    TimeSpan BaseDailyCapacity,
    TimeOnly? RequiredWakeTime);