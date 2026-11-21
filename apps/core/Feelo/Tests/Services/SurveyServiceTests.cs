using App.Commands;
using App.Interfaces;
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
public class SurveyServiceTests
{
    private readonly DBContext _dbContext;

    private readonly ISurveyService _surveyService;
    private User _user = null!;
    private SubmitSurveyCommand _command = null!;

    public SurveyServiceTests()
    {
        var logger = new NullLogger<SurveyService>();
        var options = new DbContextOptionsBuilder<DBContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _dbContext = new DBContext(options);
        _surveyService = new SurveyService(logger, _dbContext);
    }

    [SetUp]
    public void Setup()
    {
        _user = new User(StringMother.Create(), StringMother.Create(), StringMother.Create());
        _command = new SubmitSurveyCommand(_user.Id,
            new SleepSchedule(new TimeOnly(23, 0), new TimeOnly(7, 0)),
            new SleepSchedule(new TimeOnly(1, 0), new TimeOnly(10, 0)),
            TimeSpan.FromHours(8), new TimeOnly(7, 0), AlarmUsage.Sometimes,
            PeakPreference.Morning, PeakPreference.AroundNoon, PeakPreference.Afternoon,
            PeakPreference.Evening, PeakPreference.NoDifference, DipPreference.From14To16);
    }

    [Test]
    public async Task Should_reject_survey_when_user_does_not_exist()
    {
        // arrange

        // act
        var result = await _surveyService.SubmitSurvey(_command);

        // assert
        result.IsFailed.Should().BeTrue();
        result.Errors.Should().ContainSingle().Which.Message.Should().Be("Пользователь не найден");
        (await _dbContext.Users.CountAsync()).Should().Be(0);
    }

    [Test]
    public async Task Should_save_survey_answers_and_complete_onboarding()
    {
        // arrange
        _dbContext.Users.Add(_user);
        await _dbContext.SaveChangesAsync();
        var before = DateTimeOffset.UtcNow;

        // act
        var result = await _surveyService.SubmitSurvey(_command);

        // assert
        result.IsSuccess.Should().BeTrue();
        _dbContext.ChangeTracker.Clear();
        var saved = await _dbContext.Users.SingleAsync();
        saved.SleepProfile.Should().Be(new SleepProfile(_command.WorkDaySleepSchedule,
            _command.FreeDaySleepSchedule, _command.DesiredSleepDuration, _command.AlarmUsage,
            _command.RequiredWakeTime));
        saved.RhythmPreferences.Should().Be(new RhythmPreferences(_command.GeneralPeak,
            _command.MentalPeak, _command.PhysicalPeak, _command.SocialPeak,
            _command.RoutinePeak, _command.AfternoonDip));
        saved.OnboardingCompletedAt.Should().NotBeNull();
        saved.OnboardingCompletedAt!.Value.Should().BeOnOrAfter(before).And.BeOnOrBefore(DateTimeOffset.UtcNow);
    }

    [Test]
    public async Task Should_replace_existing_survey_answers()
    {
        // arrange
        _user.ApplySurveyAnswers(
            new RhythmPreferences(PeakPreference.Night, PeakPreference.Night, PeakPreference.Night,
                PeakPreference.Night, PeakPreference.Night, DipPreference.NoNoticeableDip),
            new SleepProfile(_command.FreeDaySleepSchedule, _command.WorkDaySleepSchedule,
                TimeSpan.FromHours(6), AlarmUsage.None, null));
        _dbContext.Users.Add(_user);
        await _dbContext.SaveChangesAsync();

        // act
        var result = await _surveyService.SubmitSurvey(_command);

        // assert
        result.IsSuccess.Should().BeTrue();
        _dbContext.ChangeTracker.Clear();
        var saved = await _dbContext.Users.SingleAsync();
        saved.Id.Should().Be(_user.Id);
        saved.Name.Should().Be(_user.Name);
        saved.Email.Should().Be(_user.Email);
        saved.PasswordHash.Should().Be(_user.PasswordHash);
        saved.SleepProfile!.DesiredSleepDuration.Should().Be(_command.DesiredSleepDuration);
        saved.SleepProfile.WorkDays.Should().Be(_command.WorkDaySleepSchedule);
        saved.RhythmPreferences!.GeneralPeak.Should().Be(_command.GeneralPeak);
        saved.RhythmPreferences.AfternoonDip.Should().Be(_command.AfternoonDip);
    }

    [TearDown]
    public async Task TearDown()
    {
        await _dbContext.Database.EnsureDeletedAsync();
        await _dbContext.DisposeAsync();
    }
}
