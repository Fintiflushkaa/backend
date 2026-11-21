using App.Commands;
using App.Interfaces;
using Domain.Entities.Circadian;
using Domain.Entities.User;
using FluentAssertions;
using Infrastructure;
using Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Tests.ObjectMothers;

namespace Tests.Services;

[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
public class CircadianServiceTests
{
    private readonly DBContext _dbContext;
    private readonly ICircadianService _circadianService;
    private User _user = null!;
    private SleepProfile _sleepProfile = null!;
    private RhythmPreferences _preferences = null!;
    private GetCircadianCommand _command = null!;

    public CircadianServiceTests()
    {
        var options = new DbContextOptionsBuilder<DBContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        _dbContext = new DBContext(options);
        _circadianService = new CircadianService(NullLogger<CircadianService>.Instance, _dbContext);
    }

    [SetUp]
    public void Setup()
    {
        _user = new User(StringMother.Create(), StringMother.Create(), StringMother.Create());
        _sleepProfile = new SleepProfile(
            new SleepSchedule(new TimeOnly(22, 0), new TimeOnly(8, 0)),
            new SleepSchedule(new TimeOnly(23, 0), new TimeOnly(10, 0)),
            TimeSpan.FromHours(8), AlarmUsage.None, null);
        _preferences = new RhythmPreferences(PeakPreference.NoDifference, PeakPreference.Morning,
            PeakPreference.Afternoon, PeakPreference.Evening, PeakPreference.NoDifference,
            DipPreference.From14To16);
        _command = new GetCircadianCommand(_user.Id, new DateOnly(2026, 11, 23));
    }

    [Test]
    public async Task Should_reject_calculation_when_user_does_not_exist()
    {
        // arrange

        // act
        var result = await _circadianService.GetCircadianCurves(_command);

        // assert
        result.IsFailed.Should().BeTrue();
        result.Errors.Should().ContainSingle().Which.Message.Should().Be("Пользователь не найден");
    }

    [Test]
    public async Task Should_reject_calculation_when_survey_is_missing()
    {
        // arrange
        _dbContext.Users.Add(_user);
        await _dbContext.SaveChangesAsync();

        // act
        var result = await _circadianService.GetCircadianCurves(_command);

        // assert
        result.IsFailed.Should().BeTrue();
        result.Errors.Should().ContainSingle().Which.Message.Should().Be("Сначала заполните режим сна и опросник");
    }

    [Test]
    public async Task Should_return_all_curves_for_requested_date_in_utc()
    {
        // arrange
        _user.ApplySurveyAnswers(_preferences, _sleepProfile);
        _dbContext.Users.Add(_user);
        await _dbContext.SaveChangesAsync();
        var date = _command.Date!.Value;

        // act
        var result = await _circadianService.GetCircadianCurves(_command);

        // assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Select(c => c.Kind).Should().BeEquivalentTo(Enum.GetValues<EnergyCurveKind>());
        foreach (var curve in result.Value)
        {
            curve.Points[0].Time.Should().Be(new DateTimeOffset(date.ToDateTime(new TimeOnly(8, 0)), TimeSpan.Zero));
            curve.Points[^1].Time.Should().Be(new DateTimeOffset(date.ToDateTime(new TimeOnly(22, 0)), TimeSpan.Zero));
            curve.Points.Should().OnlyContain(p => p.Time.Offset == TimeSpan.Zero);
        }
    }

    [Test]
    public async Task Should_use_current_utc_date_when_date_is_not_provided()
    {
        // arrange
        _user.ApplySurveyAnswers(_preferences, _sleepProfile);
        _dbContext.Users.Add(_user);
        await _dbContext.SaveChangesAsync();
        _command = _command with { Date = null };
        var before = DateOnly.FromDateTime(DateTime.UtcNow);

        // act
        var result = await _circadianService.GetCircadianCurves(_command);

        // assert
        result.IsSuccess.Should().BeTrue();
        var after = DateOnly.FromDateTime(DateTime.UtcNow);
        result.Value.Should().OnlyContain(c =>
            DateOnly.FromDateTime(c.Points[0].Time.UtcDateTime) == before ||
            DateOnly.FromDateTime(c.Points[0].Time.UtcDateTime) == after);
    }

    [Test]
    public async Task Should_return_validation_error_for_invalid_sleep_profile()
    {
        // arrange
        _user.ApplySurveyAnswers(_preferences, _sleepProfile with { DesiredSleepDuration = TimeSpan.Zero });
        _dbContext.Users.Add(_user);
        await _dbContext.SaveChangesAsync();

        // act
        var result = await _circadianService.GetCircadianCurves(_command);

        // assert
        result.IsFailed.Should().BeTrue();
        result.Errors.Should().ContainSingle().Which.Message.Should().Be("Желаемая длительность сна должна быть больше нуля");
    }

    [TearDown]
    public async Task TearDown()
    {
        await _dbContext.Database.EnsureDeletedAsync();
        await _dbContext.DisposeAsync();
    }
}
