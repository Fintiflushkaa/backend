using Domain.Entities.User;

namespace App.Commands;

public sealed record SubmitSurveyCommand(
    Guid UserId,
    SleepSchedule WorkDaySleepSchedule,
    SleepSchedule FreeDaySleepSchedule,
    TimeSpan DesiredSleepDuration,
    TimeOnly? RequiredWakeTime,
    AlarmUsage AlarmUsage,
    PeakPreference GeneralPeak,
    PeakPreference MentalPeak,
    PeakPreference PhysicalPeak,
    PeakPreference SocialPeak,
    PeakPreference RoutinePeak,
    DipPreference AfternoonDip);