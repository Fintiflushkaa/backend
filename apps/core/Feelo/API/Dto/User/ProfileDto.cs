namespace API.Dto.User;

public sealed record ProfileDto(
    Guid Id,
    string Name,
    SleepProfileDto? SleepProfile,
    RhythmPreferencesDto? RhythmPreferences,
    PlanningSettingsDto? PlanningSettings,
    DateTimeOffset? OnboardingCompletedAt);