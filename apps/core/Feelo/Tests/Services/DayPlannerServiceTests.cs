using App.Commands;
using App.Interfaces;
using Domain.Entities.Task;
using Domain.Entities.User;
using FluentAssertions;
using Infrastructure;
using Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Tests.ObjectMothers;
using DomainTask = Domain.Entities.Task.Task;
using Task = System.Threading.Tasks.Task;

namespace Tests.Services;

[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
public class DayPlannerServiceTests
{
    private readonly DBContext _dbContext;
    private readonly IPlannerService _plannerService;
    private User _user = null!;
    private CreateTaskCommand _command = null!;
    private DateOnly _date;

    public DayPlannerServiceTests()
    {
        var options = new DbContextOptionsBuilder<DBContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        _dbContext = new DBContext(options);
        _plannerService = new DayPlannerService(NullLogger<DayPlannerService>.Instance, _dbContext);
    }

    [SetUp]
    public void Setup()
    {
        _user = new User(StringMother.Create(), StringMother.Create(), StringMother.Create());
        _user.ApplySurveyAnswers(
            new RhythmPreferences(PeakPreference.NoDifference, PeakPreference.NoDifference,
                PeakPreference.NoDifference, PeakPreference.NoDifference, PeakPreference.NoDifference,
                DipPreference.NoNoticeableDip),
            new SleepProfile(
                new SleepSchedule(new TimeOnly(22, 0), new TimeOnly(8, 0)),
                new SleepSchedule(new TimeOnly(23, 0), new TimeOnly(10, 0)),
                TimeSpan.FromHours(8), AlarmUsage.None, null));
        _date = GetFutureDate(DayOfWeek.Monday);
        _command = new CreateTaskCommand(Guid.NewGuid(), _user.Id, StringMother.Create(),
            StringMother.Create(), TaskCategory.Mental, _date, null, TimeSpan.FromMinutes(30),
            TaskPriority.Normal, null);
    }

    [Test]
    public async Task Should_return_empty_result_for_empty_batch()
    {
        // arrange

        // act
        var result = await _plannerService.PlanMultipleTasks([], _user);

        // assert
        result.IsSuccess.Should().BeTrue();
        result.Value.PlannedTasks.Should().BeEmpty();
        result.Value.UnplannedTasks.Should().BeEmpty();
        (await _dbContext.Tasks.CountAsync()).Should().Be(0);
    }

    [Test]
    public async Task Should_reject_planning_when_survey_is_missing()
    {
        // arrange
        _user = new User(StringMother.Create(), StringMother.Create(), StringMother.Create());
        _command = _command with { UserId = _user.Id };

        // act
        var result = await _plannerService.PlanTask(_command, _user);

        // assert
        result.IsFailed.Should().BeTrue();
        result.Errors.Should().ContainSingle().Which.Message.Should().Be("Сначала заполните опросник");
        (await _dbContext.Tasks.CountAsync()).Should().Be(0);
    }

    [Test]
    public async Task Should_reject_batch_containing_another_users_task()
    {
        // arrange
        var foreignCommand = _command with { Id = Guid.NewGuid(), UserId = Guid.NewGuid() };

        // act
        var result = await _plannerService.PlanMultipleTasks([_command, foreignCommand], _user);

        // assert
        result.IsFailed.Should().BeTrue();
        result.Errors.Should().ContainSingle().Which.Message.Should().Be("В списке есть задачи другого пользователя");
        (await _dbContext.Tasks.CountAsync()).Should().Be(0);
    }

    [Test]
    public async Task Should_reject_batch_with_duplicate_task_ids()
    {
        // arrange
        var duplicate = _command with { Name = StringMother.Create() };

        // act
        var result = await _plannerService.PlanMultipleTasks([_command, duplicate], _user);

        // assert
        result.IsFailed.Should().BeTrue();
        result.Errors.Should().ContainSingle().Which.Message.Should().Be("В списке повторяются идентификаторы задач");
        (await _dbContext.Tasks.CountAsync()).Should().Be(0);
    }

    [Test]
    public async Task Should_plan_valid_task_and_report_invalid_task_in_same_batch()
    {
        // arrange
        _dbContext.Users.Add(_user);
        await _dbContext.SaveChangesAsync();
        var invalid = _command with { Id = Guid.NewGuid(), Date = default };

        // act
        var result = await _plannerService.PlanMultipleTasks([invalid, _command], _user);

        // assert
        result.IsSuccess.Should().BeTrue();
        result.Value.PlannedTasks.Should().ContainSingle().Which.Id.Should().Be(_command.Id);
        var unplanned = result.Value.UnplannedTasks.Should().ContainSingle().Subject;
        unplanned.TaskId.Should().Be(invalid.Id);
        unplanned.Reason.Should().Be("Укажите дату задачи");
        _dbContext.ChangeTracker.Clear();
        (await _dbContext.Tasks.SingleAsync()).Id.Should().Be(_command.Id);
    }

    [Test]
    public async Task Should_save_tasks_on_each_requested_date()
    {
        // arrange
        _dbContext.Users.Add(_user);
        await _dbContext.SaveChangesAsync();
        var nextDayCommand = _command with { Id = Guid.NewGuid(), Date = _date.AddDays(1) };

        // act
        var result = await _plannerService.PlanMultipleTasks([nextDayCommand, _command], _user);

        // assert
        result.IsSuccess.Should().BeTrue();
        result.Value.UnplannedTasks.Should().BeEmpty();
        _dbContext.ChangeTracker.Clear();
        var saved = await _dbContext.Tasks.ToListAsync();
        saved.Should().HaveCount(2);
        foreach (var command in new[] { _command, nextDayCommand })
        {
            var task = saved.Single(t => t.Id == command.Id);
            task.Date.Should().Be(command.Date);
            task.From.Should().BeOnOrAfter(At(command.Date, 8)).And.BeOnOrBefore(At(command.Date, 22) - command.Duration);
            task.From.Kind.Should().Be(DateTimeKind.Utc);
            task.Duration.Should().Be(command.Duration);
            task.To.Should().Be(task.From + command.Duration);
            task.Name.Should().Be(command.Name);
            task.Description.Should().Be(command.Description);
            task.Type.Should().Be(command.Type);
            task.Priority.Should().Be(command.Priority);
            task.OwnerId.Should().Be(_user.Id);
        }
    }

    [Test]
    public async Task Should_keep_fifteen_minute_gap_and_ignore_other_users_tasks()
    {
        // arrange
        _dbContext.Users.Add(_user);
        var existing = new DomainTask(Guid.NewGuid(), _user.Id, StringMother.Create(),
            TaskCategory.Mental, TimeSpan.FromHours(13), At(_date, 8), TaskPriority.Normal, _date);
        var otherUser = new User(StringMother.Create(), StringMother.Create(), StringMother.Create());
        _dbContext.Users.Add(otherUser);
        var foreignTask = new DomainTask(Guid.NewGuid(), otherUser.Id, StringMother.Create(),
            TaskCategory.Mental, TimeSpan.FromHours(14), At(_date, 8), TaskPriority.Normal, _date);
        _dbContext.Tasks.AddRange(existing, foreignTask);
        await _dbContext.SaveChangesAsync();

        // act
        var result = await _plannerService.PlanTask(_command, _user);

        // assert
        result.IsSuccess.Should().BeTrue();
        var planned = result.Value.PlannedTasks.Should().ContainSingle().Subject;
        planned.From.Should().BeOnOrAfter(existing.To.AddMinutes(15));
        planned.To.Should().BeOnOrBefore(At(_date, 22));
        (planned.From.Ticks % TimeSpan.FromMinutes(15).Ticks).Should().Be(0);
    }

    [Test]
    public async Task Should_report_unplanned_task_when_day_is_full()
    {
        // arrange
        _dbContext.Users.Add(_user);
        var existing = new DomainTask(Guid.NewGuid(), _user.Id, StringMother.Create(),
            TaskCategory.Mental, TimeSpan.FromHours(14), At(_date, 8), TaskPriority.Normal, _date);
        _dbContext.Tasks.Add(existing);
        await _dbContext.SaveChangesAsync();

        // act
        var result = await _plannerService.PlanMultipleTasks([_command], _user);

        // assert
        result.IsSuccess.Should().BeTrue();
        result.Value.PlannedTasks.Should().BeEmpty();
        var unplanned = result.Value.UnplannedTasks.Should().ContainSingle().Subject;
        unplanned.TaskId.Should().Be(_command.Id);
        unplanned.Reason.Should().Be("Нет свободного места до конца дня или дедлайна");
        (await _dbContext.Tasks.CountAsync()).Should().Be(1);
    }

    [Test]
    public async Task Should_reject_single_task_when_only_available_slots_are_after_deadline()
    {
        // arrange
        _dbContext.Users.Add(_user);
        _command = _command with { Deadline = At(_date, 9) };
        var existing = new DomainTask(Guid.NewGuid(), _user.Id, StringMother.Create(),
            TaskCategory.Mental, TimeSpan.FromHours(2), At(_date, 8), TaskPriority.Normal, _date);
        _dbContext.Tasks.Add(existing);
        await _dbContext.SaveChangesAsync();

        // act
        var result = await _plannerService.PlanTask(_command, _user);

        // assert
        result.IsFailed.Should().BeTrue();
        result.Errors.Should().ContainSingle().Which.Message.Should().Be("Нет свободного места до конца дня или дедлайна");
        (await _dbContext.Tasks.CountAsync()).Should().Be(1);
    }

    [Test]
    public async Task Should_plan_higher_priority_task_first_when_both_do_not_fit()
    {
        // arrange
        _dbContext.Users.Add(_user);
        await _dbContext.SaveChangesAsync();
        var highPriority = _command with
        {
            Id = Guid.NewGuid(), Priority = TaskPriority.High, Duration = TimeSpan.FromHours(14)
        };
        _command = _command with { Priority = TaskPriority.Low };

        // act
        var result = await _plannerService.PlanMultipleTasks([_command, highPriority], _user);

        // assert
        result.IsSuccess.Should().BeTrue();
        result.Value.PlannedTasks.Should().ContainSingle().Which.Id.Should().Be(highPriority.Id);
        result.Value.UnplannedTasks.Should().ContainSingle().Which.TaskId.Should().Be(_command.Id);
        (await _dbContext.Tasks.SingleAsync()).Id.Should().Be(highPriority.Id);
    }

    [Test]
    public async Task Should_find_new_start_without_blocking_on_same_task_or_saving_changes()
    {
        // arrange
        _dbContext.Users.Add(_user);
        _command = _command with { Duration = TimeSpan.FromHours(14) };
        var existing = new DomainTask(_command.Id, _user.Id, _command.Name, _command.Type,
            _command.Duration, At(_date, 8), _command.Priority, _date);
        _dbContext.Tasks.Add(existing);
        await _dbContext.SaveChangesAsync();

        // act
        var result = await _plannerService.FindStartForTask(_command, _user);

        // assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(At(_date, 8));
        _dbContext.ChangeTracker.HasChanges().Should().BeFalse();
        _dbContext.ChangeTracker.Clear();
        var saved = await _dbContext.Tasks.SingleAsync();
        saved.From.Should().Be(existing.From);
        saved.Version.Should().Be(existing.Version);
    }

    [Test]
    public async Task Should_not_plan_tasks_in_the_past()
    {
        // arrange
        _command = _command with { Date = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-1) };

        // act
        var result = await _plannerService.PlanTask(_command, _user);

        // assert
        result.IsFailed.Should().BeTrue();
        result.Errors.Should().ContainSingle().Which.Message.Should().Be("Нет свободного места до конца дня или дедлайна");
        (await _dbContext.Tasks.CountAsync()).Should().Be(0);
    }

    [Test]
    public async Task Should_not_plan_task_when_it_does_not_feet_before_bedtime()
    {
        // arrange
        _command = _command with { Duration = TimeSpan.FromHours(3) };
        _dbContext.Users.Add(_user);
        var existing = new DomainTask(Guid.NewGuid(), _user.Id, StringMother.Create(),
            TaskCategory.Mental, TimeSpan.FromHours(11.75), At(_date, 8), TaskPriority.Normal, _date);
        _dbContext.Tasks.Add(existing);
        await _dbContext.SaveChangesAsync();

        // act
        var result = await _plannerService.PlanTask(_command, _user);

        // assert
        result.IsFailed.Should().BeTrue();
        result.Errors.Should().ContainSingle().Which.Message.Should().Be("Нет свободного места до конца дня или дедлайна");
        _dbContext.ChangeTracker.Clear();
        (await _dbContext.Tasks.SingleAsync()).Id.Should().Be(existing.Id);
    }

    [Test]
    public async Task Should_limit_sunday_evening_planning_to_allow_sleep_before_monday_wake_time()
    {
        // arrange
        _date = GetFutureDate(DayOfWeek.Sunday);
        _user.ApplySurveyAnswers(_user.RhythmPreferences!, new SleepProfile(
            new SleepSchedule(new TimeOnly(1, 0), new TimeOnly(8, 0)),
            new SleepSchedule(new TimeOnly(5, 0), new TimeOnly(12, 0)),
            TimeSpan.FromHours(7), AlarmUsage.None, null));
        _command = _command with { Date = _date, Duration = TimeSpan.FromHours(2), Priority = TaskPriority.High };
        var extraCommand = _command with
        {
            Id = Guid.NewGuid(), Duration = TimeSpan.FromMinutes(30), Priority = TaskPriority.Low
        };
        _dbContext.Users.Add(_user);
        var existing = new DomainTask(Guid.NewGuid(), _user.Id, StringMother.Create(),
            TaskCategory.Mental, TimeSpan.FromHours(10.75), At(_date, 12), TaskPriority.Normal, _date);
        _dbContext.Tasks.Add(existing);
        await _dbContext.SaveChangesAsync();

        // act
        var result = await _plannerService.PlanMultipleTasks([extraCommand, _command], _user);

        // assert
        result.IsSuccess.Should().BeTrue();
        var planned = result.Value.PlannedTasks.Should().ContainSingle().Subject;
        planned.Id.Should().Be(_command.Id);
        planned.Date.Should().Be(_date);
        planned.From.Should().Be(At(_date, 23));
        planned.To.Should().Be(At(_date.AddDays(1), 1));
        (At(_date.AddDays(1), 8) - planned.To).Should().Be(TimeSpan.FromHours(7));
        result.Value.UnplannedTasks.Should().ContainSingle().Which.TaskId.Should().Be(extraCommand.Id);
        _dbContext.ChangeTracker.Clear();
        (await _dbContext.Tasks.Select(t => t.Id).ToListAsync()).Should().BeEquivalentTo([existing.Id, _command.Id]);
    }

    [Test]
    public async Task Should_allow_late_friday_planning_using_saturday_sleep_schedule()
    {
        // arrange
        _date = GetFutureDate(DayOfWeek.Friday);
        _user.ApplySurveyAnswers(_user.RhythmPreferences!, new SleepProfile(
            new SleepSchedule(new TimeOnly(0, 0), new TimeOnly(8, 0)),
            new SleepSchedule(new TimeOnly(2, 0), new TimeOnly(9, 0)),
            TimeSpan.FromHours(8), AlarmUsage.None, null));
        _command = _command with { Date = _date, Duration = TimeSpan.FromHours(2) };
        _dbContext.Users.Add(_user);
        var existing = new DomainTask(Guid.NewGuid(), _user.Id, StringMother.Create(),
            TaskCategory.Mental, TimeSpan.FromHours(14.75), At(_date, 8), TaskPriority.Normal, _date);
        _dbContext.Tasks.Add(existing);
        await _dbContext.SaveChangesAsync();

        // act
        var result = await _plannerService.PlanTask(_command, _user);

        // assert
        result.IsSuccess.Should().BeTrue();
        result.Value.UnplannedTasks.Should().BeEmpty();
        var planned = result.Value.PlannedTasks.Should().ContainSingle().Subject;
        planned.Id.Should().Be(_command.Id);
        planned.Date.Should().Be(_date);
        planned.From.Should().Be(At(_date, 23));
        planned.To.Should().Be(At(_date.AddDays(1), 1));
        (At(_date.AddDays(1), 9) - planned.To).Should().Be(TimeSpan.FromHours(8));
        _dbContext.ChangeTracker.Clear();
        var saved = await _dbContext.Tasks.SingleAsync(t => t.Id == _command.Id);
        saved.From.Should().Be(planned.From);
        saved.To.Should().Be(planned.To);
    }

    [TearDown]
    public async Task TearDown()
    {
        await _dbContext.Database.EnsureDeletedAsync();
        await _dbContext.DisposeAsync();
    }

    private static DateOnly GetFutureDate(DayOfWeek dayOfWeek)
    {
        var date = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(7);
        while (date.DayOfWeek != dayOfWeek)
            date = date.AddDays(1);
        return date;
    }

    private static DateTime At(DateOnly date, int hour) => date.ToDateTime(new TimeOnly(hour, 0), DateTimeKind.Utc);
}
