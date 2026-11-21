using Domain.Entities.User;
using FluentAssertions;
using Infrastructure.Services;

namespace Tests.Services;

[TestFixture]
public class CircadianCalculatorTests
{
    private SleepProfile _sleepProfile = null!;
    private RhythmPreferences _preferences = null!;
    private DateOnly _date;

    [SetUp]
    public void Setup()
    {
        var schedule = new SleepSchedule(new TimeOnly(23, 0), new TimeOnly(7, 0));
        _sleepProfile = new SleepProfile(schedule, schedule, TimeSpan.FromHours(8), AlarmUsage.None, null);
        _preferences = new RhythmPreferences(PeakPreference.NoDifference, PeakPreference.NoDifference,
            PeakPreference.NoDifference, PeakPreference.NoDifference, PeakPreference.NoDifference,
            DipPreference.NoNoticeableDip);
        _date = new DateOnly(2026, 11, 23);
    }

    [Test]
    public void Should_calculate_mid_sleep_across_midnight()
    {
        // arrange

        // act
        var result = CircadianCalculator.CalculateMsfSc(_sleepProfile);

        // assert
        result.Should().BeApproximately(3, 0.000001);
    }

    [Test]
    public void Should_correct_mid_sleep_for_longer_weekend_sleep()
    {
        // arrange
        _sleepProfile = _sleepProfile with
        {
            FreeDays = new SleepSchedule(new TimeOnly(1, 0), new TimeOnly(10, 0))
        };

        // act
        var result = CircadianCalculator.CalculateMsfSc(_sleepProfile);

        // assert
        result.Should().BeApproximately(5.142857, 0.000001);
    }

    [Test]
    public void Should_reject_equal_sleep_and_wake_times()
    {
        // arrange
        _sleepProfile = _sleepProfile with
        {
            WorkDays = new SleepSchedule(new TimeOnly(7, 0), new TimeOnly(7, 0))
        };

        // act
        Action act = () => CircadianCalculator.CalculateMsfSc(_sleepProfile);

        // assert
        act.Should().Throw<ArgumentException>().WithMessage("Время сна и пробуждения не должны совпадать");
    }

    [Test]
    public void Should_end_sunday_curves_early_enough_for_monday_sleep()
    {
        // arrange
        _date = new DateOnly(2026, 11, 22);
        _sleepProfile = new SleepProfile(
            new SleepSchedule(new TimeOnly(1, 0), new TimeOnly(8, 0)),
            new SleepSchedule(new TimeOnly(3, 0), new TimeOnly(10, 0)),
            TimeSpan.FromHours(7), AlarmUsage.None, null);
        var latestBedtime = new DateTimeOffset(2026, 11, 23, 1, 0, 0, TimeSpan.Zero);

        // act
        var result = CircadianCalculator.Calculate(_sleepProfile, _preferences, _date, TimeZoneInfo.Utc);

        // assert
        result.Should().OnlyContain(c => c.Points[c.Points.Count - 1].Time == latestBedtime);
    }
}
