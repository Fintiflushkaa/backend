using API.Dto.User;

namespace API.Dto;

public sealed record SurveyAnswerDto(
    SleepScheduleDto WorkDaySleepSchedule,
    SleepScheduleDto FreeDaySleepSchedule,
    TimeSpan DesiredSleepDuration,
    TimeOnly? RequiredWakeTime,
    AlarmUsage AlarmUsage,
    PeakPreference GeneralPeak,
    PeakPreference MentalPeak,
    PeakPreference PhysicalPeak,
    PeakPreference SocialPeak,
    PeakPreference RoutinePeak,
    DipPreference AfternoonDip);