namespace API.Dto.User;

public record RhythmPreferencesDto(
    PeakPreference GeneralPeak,
    PeakPreference MentalPeak,
    PeakPreference PhysicalPeak,
    PeakPreference SocialPeak,
    PeakPreference RoutinePeak,
    DipPreference AfternoonDip);