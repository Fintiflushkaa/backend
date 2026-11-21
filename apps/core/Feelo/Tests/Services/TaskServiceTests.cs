using System.Reflection;
using App.Commands;
using App.Interfaces;
using App.Planning;
using App.Synchronization;
using Domain.Entities.Task;
using Domain.Entities.User;
using FluentAssertions;
using FluentResults;
using Infrastructure;
using Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Tests.ObjectMothers;
using DomainTask = Domain.Entities.Task.Task;
using Task = System.Threading.Tasks.Task;

namespace Tests.Services;

[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
public class TaskServiceTests
{
    private readonly DBContext _dbContext;
    private readonly IPlannerService _plannerService;
    private readonly ITaskService _taskService;
    private User _user = null!;
    private User _anotherUser = null!;
    private DomainTask _task = null!;
    private CreateTaskCommand _createCommand = null!;
    private UpdateTaskCommand _updateCommand = null!;
    private DateOnly _date;
    private DateTime _from;
    private DateTime _deadline;

    public TaskServiceTests()
    {
        var options = new DbContextOptionsBuilder<DBContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning)).Options;
        _dbContext = new DBContext(options);
        _plannerService = Substitute.For<IPlannerService>();
        _taskService = new TestTaskService(_dbContext, _plannerService);
    }

    [SetUp]
    public void Setup()
    {
        _date = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(7);
        _from = _date.ToDateTime(new TimeOnly(10, 0), DateTimeKind.Utc);
        _deadline = _from.AddHours(8);
        _user = new User(StringMother.Create(), Guid.NewGuid()+"@test", StringMother.Create());
        _anotherUser = new User(StringMother.Create(), Guid.NewGuid()+"@test", StringMother.Create());
        _task = new DomainTask(Guid.NewGuid(), _user.Id, StringMother.Create(), TaskCategory.Mental,
            TimeSpan.FromMinutes(30), _from, TaskPriority.Normal, _date, _deadline, StringMother.Create());
        _createCommand = new CreateTaskCommand(_task.Id, _user.Id, _task.Name, _task.Description,
            _task.Type, _date, _from, _task.Duration, _task.Priority, _deadline);
        _updateCommand = new UpdateTaskCommand(_user.Id, _task.Id, _task.Version);
    }

    [Test]
    public async Task Should_reject_create_sync_validation_for_empty_list()
    {
        // arrange

        // act
        var result = await _taskService.SyncTasks([]);

        // assert
        result.IsFailed.Should().BeTrue();
        result.Errors.Should().ContainSingle().Which.Message.Should().Be("Список задач пуст");
    }

    [Test]
    public async Task Should_reject_create_sync_validation_for_missing_user()
    {
        // arrange

        // act
        var result = await InvokeValidation("ValidateCreateTaskSync", [_createCommand]);

        // assert
        result.IsFailed.Should().BeTrue();
        result.Errors.Should().ContainSingle().Which.Message.Should().Be("Пользователь не найден");
    }

    [Test]
    public async Task Should_reject_create_sync_validation_for_different_task_owners()
    {
        // arrange
        await SeedUser();

        // act
        var result = await InvokeValidation("ValidateCreateTaskSync", [_createCommand, _createCommand with { Id = Guid.NewGuid(), UserId = _anotherUser.Id }]);

        // assert
        result.IsFailed.Should().BeTrue();
        result.Errors.Should().ContainSingle().Which.Message.Should().Be("У одной из задач отличается создатель");
    }

    [Test]
    public async Task Should_reject_create_sync_validation_for_empty_task_id()
    {
        // arrange
        await SeedUser();

        // act
        var result = await InvokeValidation("ValidateCreateTaskSync", [_createCommand with { Id = Guid.Empty }]);

        // assert
        result.IsFailed.Should().BeTrue();
        result.Errors.Should().ContainSingle().Which.Message.Should().Be("Некорректный ID задачи");
    }

    [Test]
    public async Task Should_reject_create_sync_validation_for_missing_date()
    {
        // arrange
        await SeedUser();

        // act
        var result = await InvokeValidation("ValidateCreateTaskSync", [_createCommand with { Date = default }]);

        // assert
        result.IsFailed.Should().BeTrue();
        result.Errors.Should().ContainSingle().Which.Message.Should().Be("Укажите дату задачи");
    }

    [Test]
    public async Task Should_reject_create_sync_validation_for_nonpositive_duration()
    {
        // arrange
        await SeedUser();

        // act
        var result = await InvokeValidation("ValidateCreateTaskSync", [_createCommand with { Duration = TimeSpan.Zero }]);

        // assert
        result.IsFailed.Should().BeTrue();
        result.Errors.Should().ContainSingle().Which.Message.Should().Be("Длительность задачи не может быть отрицательной");
    }

    [Test]
    public async Task Should_reject_create_sync_validation_for_duplicate_task_ids()
    {
        // arrange
        await SeedUser();

        // act
        var result = await InvokeValidation("ValidateCreateTaskSync", [_createCommand, _createCommand]);

        // assert
        result.IsFailed.Should().BeTrue();
        result.Errors.Should().ContainSingle().Which.Message.Should()
            .Be($"Повторяющиеся ID: {_task.Id}");
    }

    [Test]
    public async Task Should_accept_create_sync_validation_for_valid_commands()
    {
        // arrange
        await SeedUser();

        // act
        var result = await InvokeValidation("ValidateCreateTaskSync", [_createCommand]);

        // assert
        result.IsSuccess.Should().BeTrue();
        result.Errors.Should().BeEmpty();
    }

    [Test]
    public async Task Should_reject_update_validation_for_missing_user()
    {
        // arrange

        // act
        var result = await _taskService.UpdateTask(_updateCommand);

        // assert
        result.IsFailed.Should().BeTrue();
        result.Errors.Should().ContainSingle().Which.Message.Should().Be("Пользователь не найден");
    }

    [Test]
    public async Task Should_reject_update_validation_for_negative_duration()
    {
        // arrange
        await SeedUser();

        // act
        var result = await _taskService.UpdateTask(_updateCommand with { Duration = TimeSpan.FromMinutes(-1) });

        // assert
        result.IsFailed.Should().BeTrue();
        result.Errors.Should().ContainSingle().Which.Message.Should().Be("Длительность задачи не может быть отрицательной");
    }

    [Test]
    public async Task Should_reject_creation_with_negative_duration_before_planning()
    {
        // arrange
        var command = _createCommand with { Duration = TimeSpan.FromMinutes(-1) };

        // act
        var result = await _taskService.CreateTask(command);

        // assert
        result.Errors.Should().ContainSingle().Which.Message.Should().Be("Длительность задачи не может быть отрицательной");
        await _plannerService.DidNotReceiveWithAnyArgs().PlanTask(null!, null!);
    }

    [Test]
    public async Task Should_reject_creation_without_date_before_planning()
    {
        // arrange

        // act
        var result = await _taskService.CreateTask(_createCommand with { Date = default });

        // assert
        result.Errors.Should().ContainSingle().Which.Message.Should().Be("Укажите дату задачи");
        await _plannerService.DidNotReceiveWithAnyArgs().PlanTask(null!, null!);
    }

    [Test]
    public async Task Should_return_error_when_getting_tasks_for_missing_user()
    {
        // arrange

        // act
        var result = await _taskService.GetAllTasks(new GetAllTasksCommand(_user.Id));

        // assert
        result.Errors.Should().ContainSingle().Which.Message.Should().Be("Пользователь не найден");
    }

    [Test]
    public async Task Should_return_empty_list_for_user_without_tasks()
    {
        // arrange
        await SeedUser();

        // act
        var result = await _taskService.GetAllTasks(new GetAllTasksCommand(_user.Id));

        // assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeEmpty();
    }

    [Test]
    public async Task Should_return_only_tasks_owned_by_requested_user()
    {
        // arrange
        await SeedTask();
        _dbContext.Users.Add(_anotherUser);
        _dbContext.Tasks.Add(new DomainTask(Guid.NewGuid(), _anotherUser.Id, "other", _task.Type,
            _task.Duration, _from, _task.Priority, _date));
        await _dbContext.SaveChangesAsync();

        // act
        var result = await _taskService.GetAllTasks(new GetAllTasksCommand(_user.Id));

        // assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().ContainSingle().Which.Should().BeEquivalentTo(_task);
    }

    [Test]
    public async Task Should_not_create_task_for_missing_user()
    {
        // arrange

        // act
        var result = await _taskService.CreateTask(_createCommand);

        // assert
        result.Errors.Should().ContainSingle().Which.Message.Should().Be("Пользователь не найден");
        (await _dbContext.Tasks.CountAsync()).Should().Be(0);
    }

    [Test]
    public async Task Should_save_manual_task_with_calculated_end_time()
    {
        // arrange
        await SeedUser();

        // act
        var result = await _taskService.CreateTask(_createCommand);

        // assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Item1.Should().Be(_task.Id.ToString());
        _dbContext.ChangeTracker.Clear();
        var saved = await _dbContext.Tasks.SingleAsync();
        saved.Name.Should().Be(_createCommand.Name);
        saved.OwnerId.Should().Be(_user.Id);
        saved.From.Should().Be(_from);
        saved.To.Should().Be(_from + _createCommand.Duration);
        saved.Date.Should().Be(_date);
        saved.Version.Should().Be(0);
        await _plannerService.DidNotReceiveWithAnyArgs().PlanTask(null!, null!);
    }

    [Test]
    public async Task Should_reject_creation_with_existing_task_id()
    {
        // arrange
        await SeedTask();

        // act
        var result = await _taskService.CreateTask(_createCommand);

        // assert
        result.IsFailed.Should().BeTrue();
        (await _dbContext.Tasks.CountAsync()).Should().Be(1);
        await _plannerService.DidNotReceiveWithAnyArgs().PlanTask(null!, null!);
    }

    [Test]
    public async Task Should_return_task_created_by_planner()
    {
        // arrange
        await SeedUser();
        var command = _createCommand with { From = null };
        _plannerService.PlanTask(command, _user).Returns(async _ =>
        {
            _dbContext.Tasks.Add(_task);
            await _dbContext.SaveChangesAsync();
            return Result.Ok(new PlanningResult([_task], []));
        });

        // act
        var result = await _taskService.CreateTask(command);

        // assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Item2.Should().BeSameAs(_task);
        (await _dbContext.Tasks.CountAsync()).Should().Be(1);
    }

    [Test]
    public async Task Should_return_planning_error_without_creating_task()
    {
        // arrange
        await SeedUser();
        var command = _createCommand with { From = null };
        _plannerService.PlanTask(command, _user).Returns(Result.Fail<PlanningResult>("No available slot"));

        // act
        var result = await _taskService.CreateTask(command);

        // assert
        result.Errors.Should().ContainSingle().Which.Message.Should().Be("No available slot");
        (await _dbContext.Tasks.CountAsync()).Should().Be(0);
    }

    [TestCase(false)]
    [TestCase(true)]
    public async Task Should_return_synced_without_duplicating_repeated_creation(bool automatic)
    {
        // arrange
        await SeedTask();
        var command = automatic ? _createCommand with { From = null } : _createCommand;

        // act
        var result = await _taskService.SyncTasks([command]);

        // assert
        result.Value[_task.Id].Should().Be(new TaskSyncResult(0, TaskSyncStatus.Synced));
        (await _dbContext.Tasks.CountAsync()).Should().Be(1);
        await _plannerService.DidNotReceiveWithAnyArgs().PlanMultipleTasks(null!, null!);
    }

    [Test]
    public async Task Should_create_manual_and_automatic_tasks_in_same_sync_batch()
    {
        // arrange
        await SeedUser();
        var automatic = _createCommand with { From = null };
        var manual = _createCommand with { Id = Guid.NewGuid(), Name = "manual", From = _from.AddHours(2) };
        _plannerService.PlanMultipleTasks(Arg.Any<List<CreateTaskCommand>>(), _user).Returns(async _ =>
        {
            _dbContext.Tasks.Add(_task);
            await _dbContext.SaveChangesAsync();
            return Result.Ok(new PlanningResult([_task], []));
        });

        // act
        var result = await _taskService.SyncTasks([manual, automatic]);

        // assert
        result.IsSuccess.Should().BeTrue();
        result.Value[manual.Id].Should().Be(new TaskSyncResult(0, TaskSyncStatus.Created));
        result.Value[automatic.Id].Should().Be(new TaskSyncResult(0, TaskSyncStatus.Created));
        (await _dbContext.Tasks.CountAsync()).Should().Be(2);
        await _plannerService.Received(1).PlanMultipleTasks(
            Arg.Is<List<CreateTaskCommand>>(c => c.Count == 1 && c[0].Id == automatic.Id), _user);
    }

    [Test]
    public async Task Should_return_creation_conflict_without_overwriting_existing_task()
    {
        // arrange
        await SeedTask();

        // act
        var result = await _taskService.SyncTasks([_createCommand with { Name = "changed" }]);

        // assert
        result.Value[_task.Id].Status.Should().Be(TaskSyncStatus.Conflicted);
        result.Value[_task.Id].Version.Should().Be(0);
        _dbContext.ChangeTracker.Clear();
        (await _dbContext.Tasks.FindAsync(_task.Id))!.Name.Should().Be(_createCommand.Name);
    }

    [Test]
    public async Task Should_update_task_and_increment_version_when_versions_match()
    {
        // arrange
        await SeedTask();

        // act
        var result = await _taskService.UpdateTask(_updateCommand with { Name = "changed" });

        // assert
        result.Value[_task.Id].Should().Be(new TaskSyncResult(1, TaskSyncStatus.Modified));
        _dbContext.ChangeTracker.Clear();
        var saved = (await _dbContext.Tasks.FindAsync(_task.Id))!;
        saved.Name.Should().Be("changed");
        saved.Version.Should().Be(1);
    }

    [TestCase(0)]
    [TestCase(1)]
    public async Task Should_return_synced_without_incrementing_version_when_content_matches(int clientVersion)
    {
        // arrange
        _task.UpdateTask(null, null, null, null, null, null, null, null);
        await SeedTask();
        var command = _updateCommand with { Version = clientVersion };

        // act
        var result = await _taskService.UpdateTask(command);

        // assert
        result.Value[_task.Id].Should().Be(new TaskSyncResult(1, TaskSyncStatus.Synced));
        _dbContext.ChangeTracker.Clear();
        (await _dbContext.Tasks.FindAsync(_task.Id))!.Version.Should().Be(1);
    }

    [Test]
    public async Task Should_return_server_task_without_saving_stale_update()
    {
        // arrange
        _task.UpdateTask(null, null, null, null, null, null, null, null);
        await SeedTask();
        var command = _updateCommand with { Name = "changed" };

        // act
        var result = await _taskService.UpdateTask(command);

        // assert
        var sync = result.Value[_task.Id];
        sync.Status.Should().Be(TaskSyncStatus.Conflicted);
        sync.Version.Should().Be(1);
        sync.ConflictedTask.Should().BeEquivalentTo(_task);
        _dbContext.ChangeTracker.Clear();
        (await _dbContext.Tasks.FindAsync(_task.Id))!.Name.Should().Be(_createCommand.Name);
    }

    [Test]
    public async Task Should_not_update_another_users_task()
    {
        // arrange
        await SeedTask();
        _dbContext.Users.Add(_anotherUser);
        await _dbContext.SaveChangesAsync();

        // act
        var result = await _taskService.UpdateTask(
            _updateCommand with { UserId = _anotherUser.Id, Name = "changed" });

        // assert
        result.Errors.Should().ContainSingle().Which.Message.Should().Be("Задача не найдена");
        _dbContext.ChangeTracker.Clear();
        (await _dbContext.Tasks.FindAsync(_task.Id))!.Name.Should().Be(_createCommand.Name);
    }

    [Test]
    public async Task Should_return_error_when_updating_missing_task()
    {
        // arrange
        await SeedUser();

        // act
        var result = await _taskService.UpdateTask(_updateCommand with { Name = "changed" });

        // assert
        result.Errors.Should().ContainSingle().Which.Message.Should().Be("Задача не найдена");
    }

    [Test]
    public async Task Should_replan_task_when_only_new_date_is_supplied()
    {
        // arrange
        await SeedTask();
        var date = _date.AddDays(1);
        var start = _from.AddDays(1).AddHours(3);
        _plannerService.FindStartForTask(Arg.Any<CreateTaskCommand>(), _user).Returns(Result.Ok(start));

        // act
        var result = await _taskService.UpdateTask(_updateCommand with { Date = date });

        // assert
        result.Value[_task.Id].Status.Should().Be(TaskSyncStatus.Modified);
        _dbContext.ChangeTracker.Clear();
        var saved = (await _dbContext.Tasks.FindAsync(_task.Id))!;
        saved.Date.Should().Be(date);
        saved.From.Should().Be(start);
        saved.To.Should().Be(start + _createCommand.Duration);
        await _plannerService.Received(1).FindStartForTask(
            Arg.Is<CreateTaskCommand>(c => c.Id == _task.Id && c.Date == date && c.From == null), _user);
    }

    [Test]
    public async Task Should_preserve_task_when_replanning_fails()
    {
        // arrange
        await SeedTask();
        _plannerService.FindStartForTask(Arg.Any<CreateTaskCommand>(), _user)
            .Returns(Result.Fail<DateTime>("No available slot"));

        // act
        var result = await _taskService.UpdateTask(_updateCommand with { Date = _date.AddDays(1) });

        // assert
        result.Errors.Should().ContainSingle().Which.Message.Should().Be("No available slot");
        _dbContext.ChangeTracker.Clear();
        var saved = (await _dbContext.Tasks.FindAsync(_task.Id))!;
        saved.Date.Should().Be(_date);
        saved.From.Should().Be(_from);
        saved.Version.Should().Be(0);
    }

    [Test]
    public async Task Should_clear_description_when_null_is_explicitly_requested()
    {
        // arrange
        await SeedTask();

        // act
        var result = await _taskService.UpdateTask(_updateCommand with { DescriptionSpecified = true });

        // assert
        result.Value[_task.Id].Status.Should().Be(TaskSyncStatus.Modified);
        _dbContext.ChangeTracker.Clear();
        (await _dbContext.Tasks.FindAsync(_task.Id))!.Description.Should().BeNull();
    }

    [TestCase(true)]
    [TestCase(false)]
    public async Task Should_apply_completion_state_and_keep_completion_date_consistent(bool completed)
    {
        // arrange
        if (!completed)
            _task.UpdateTask(null, null, null, null, null, null, true, _from);
        await SeedTask();
        var expectedVersion = _task.Version + 1;
        var command = _updateCommand with { Version = _task.Version, IsCompleted = completed,
            CompletedAt = completed ? _from : null };

        // act
        var result = await _taskService.UpdateTask(command);

        // assert
        result.Value[_task.Id].Status.Should().Be(TaskSyncStatus.Modified);
        _dbContext.ChangeTracker.Clear();
        var saved = (await _dbContext.Tasks.FindAsync(_task.Id))!;
        saved.IsCompleted.Should().Be(completed);
        saved.CompletedAt.Should().Be(completed ? _from : null);
        saved.Version.Should().Be(expectedVersion);
    }

    [Test]
    public async Task Should_not_delete_task_for_missing_user()
    {
        // arrange

        // act
        var result = await _taskService.DeleteTask(new DeleteTaskCommand(_task.Id, _user.Id));

        // assert
        result.Errors.Should().ContainSingle().Which.Message.Should().Be("Пользователь не найден");
    }

    [Test]
    public async Task Should_return_error_when_deleting_missing_task()
    {
        // arrange
        await SeedUser();

        // act
        var result = await _taskService.DeleteTask(new DeleteTaskCommand(_task.Id, _user.Id));

        // assert
        result.Errors.Should().ContainSingle().Which.Message.Should().Be("Задача не найдена");
    }

    [Test]
    public async Task Should_not_delete_another_users_task()
    {
        // arrange
        await SeedTask();
        _dbContext.Users.Add(_anotherUser);
        await _dbContext.SaveChangesAsync();

        // act
        var result = await _taskService.DeleteTask(new DeleteTaskCommand(_task.Id, _anotherUser.Id));

        // assert
        result.Errors.Should().ContainSingle().Which.Message.Should().Be("Задача не найдена");
        _dbContext.ChangeTracker.Clear();
        (await _dbContext.Tasks.FindAsync(_task.Id)).Should().NotBeNull();
    }

    [Test]
    public async Task Should_delete_only_requested_owned_task()
    {
        // arrange
        await SeedTask();
        var anotherTask = new DomainTask(Guid.NewGuid(), _user.Id, "keep", _task.Type,
            _task.Duration, _from, _task.Priority, _date);
        _dbContext.Tasks.Add(anotherTask);
        await _dbContext.SaveChangesAsync();

        // act
        var result = await _taskService.DeleteTask(new DeleteTaskCommand(_task.Id, _user.Id));

        // assert
        result.IsSuccess.Should().BeTrue();
        _dbContext.ChangeTracker.Clear();
        (await _dbContext.Tasks.FindAsync(_task.Id)).Should().BeNull();
        (await _dbContext.Tasks.FindAsync(anotherTask.Id)).Should().NotBeNull();
        (await _dbContext.Users.FindAsync(_user.Id)).Should().NotBeNull();
    }

    private async Task SeedUser()
    {
        _dbContext.Users.Add(_user);
        await _dbContext.SaveChangesAsync();
    }

    private async Task SeedTask()
    {
        _dbContext.Users.Add(_user);
        _dbContext.Tasks.Add(_task);
        await _dbContext.SaveChangesAsync();
    }

    private Task<Result> InvokeValidation(string name, List<CreateTaskCommand> commands) =>
        (Task<Result>)typeof(TaskService).GetMethod(name, BindingFlags.NonPublic | BindingFlags.Instance)!
            .Invoke(_taskService, [commands])!;

    private sealed class TestTaskService(DBContext context, IPlannerService planner)
        : TaskService(NullLogger<TaskService>.Instance, context, planner)
    {
        private readonly DBContext _context = context;

        protected override async Task<User?> LockUserAsync(Guid userId) =>
            await _context.Users.FindAsync(userId);
    }

    [TearDown]
    public async Task TearDown()
    {
        await _dbContext.Database.EnsureDeletedAsync();
        await _dbContext.DisposeAsync();
    }
}
