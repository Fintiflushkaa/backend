namespace Domain.Entities.User;

public sealed record RhythmPreferences(
    PeakPreference GeneralPeak,
    PeakPreference MentalPeak,
    PeakPreference PhysicalPeak,
    PeakPreference SocialPeak,
    PeakPreference RoutinePeak,
    DipPreference AfternoonDip);

public enum PeakPreference
{
    Morning,       // 06–10
    AroundNoon,    // 10–14
    Afternoon,     // 14–18
    Evening,       // 18–22
    Night,         // 22–02
    NoDifference
}

public enum DipPreference
{
    From12To14,
    From14To16,
    From16To18,
    After18,
    NoNoticeableDip
}