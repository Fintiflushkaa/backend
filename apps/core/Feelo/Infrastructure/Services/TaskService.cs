using App.Commands;
using App.Interfaces;
using App.Synchronization;
using Domain.Entities.User;
using FluentResults;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using DomainTask = Domain.Entities.Task.Task;

namespace Infrastructure.Services;

public class TaskService : ITaskService
{
    private readonly ILogger<TaskService> _logger;
    private readonly DBContext _dbContext;
    private readonly IPlannerService _plannerService;

    private const string CreateConflictMessage = "Задача с таким id уже существует";

    public TaskService(ILogger<TaskService> logger, DBContext dbContext, IPlannerService plannerService)
    {
        _logger = logger;
        _dbContext = dbContext;
        _plannerService = plannerService;
    }

    public async Task<Result<(string, DomainTask)>> CreateTask(CreateTaskCommand command)
    {
        if (command.Duration < TimeSpan.Zero)
            return Result.Fail("Длительность задачи не может быть отрицательной");

        await using var transaction = await _dbContext.Database.BeginTransactionAsync();
        var user = await LockUserAsync(command.UserId);
        if (user is null)
            return Result.Fail("Пользователь не найден");

        var result = await CreateTaskForUser(command, user);
        if (result.IsSuccess)
            await transaction.CommitAsync();
        return result;
    }

    public async Task<Result<Dictionary<Guid, TaskSyncResult>>> SyncTasks(List<CreateTaskCommand> commands)
    {
        var validation = await ValidateCreateTaskSync(commands);
        if (validation.IsFailed)
            return Result.Fail(validation.Errors);

        await using var transaction = await _dbContext.Database.BeginTransactionAsync();
        var user = await LockUserAsync(commands[0].UserId);
        if (user is null)
            return Result.Fail("Пользователь не найден");

        var result = await SyncTasksForUser(commands, user);
        await transaction.CommitAsync();
        return Result.Ok(result);
    }

    public async Task<Result<List<DomainTask>>> GetAllTasks(GetAllTasksCommand command)
    {
        var user = await _dbContext.Users.FindAsync(command.UserId);
        if (user == null)
            return Result.Fail("Пользователь не найден");
        var tasks = await _dbContext.Tasks
            .Where(x => x.OwnerId == command.UserId)
            .ToListAsync();
        _logger.LogInformation("Получены все задачи пользователя {UserId} в количестве {Count}", user.Id, tasks.Count);
        return tasks;
    }

    public async Task<Result> UpdateTask(UpdateTaskCommand command)
    {
        if (command.Duration < TimeSpan.Zero)
            return Result.Fail("Длительность задачи не может быть отрицательной");

        await using var transaction = await _dbContext.Database.BeginTransactionAsync();
        var user = await LockUserAsync(command.UserId);
        if (user is null)
            return Result.Fail("Пользователь не найден");

        var task = await FindOwnedTask(command.TaskId, command.UserId);
        if (task is null)
            return Result.Fail("Задача не найдена");

        task.UpdateTask(command.Name, command.Type, command.Duration, command.From, command.Priority,
            command.Deadline, command.Description, command.DescriptionSpecified);
        await _dbContext.SaveChangesAsync();
        _logger.LogInformation("Задача {TaskId} была обновлена для пользователя {UserId}", task.Id, user.Id);
        await transaction.CommitAsync();
        return Result.Ok();
    }

    public async Task<Result> DeleteTask(DeleteTaskCommand command)
    {
        await using var transaction = await _dbContext.Database.BeginTransactionAsync();
        var user = await LockUserAsync(command.UserId);
        if (user is null)
            return Result.Fail("Пользователь не найден");

        var task = await FindOwnedTask(command.TaskId, command.UserId);
        if (task is null)
            return Result.Fail("Задача не найдена");

        _dbContext.Tasks.Remove(task);
        await _dbContext.SaveChangesAsync();
        await transaction.CommitAsync();
        return Result.Ok();
    }

    private Task<DomainTask?> FindOwnedTask(Guid taskId, Guid userId) =>
        _dbContext.Tasks.FirstOrDefaultAsync(t => t.Id == taskId && t.OwnerId == userId);

    private async Task<Result<(string, DomainTask)>> CreateTaskForUser(CreateTaskCommand command, User user)
    {
        if (await _dbContext.Tasks.AnyAsync(t => t.Id == command.Id))
            return Result.Fail($"Задача с id {command.Id} уже существует!");

        if (command.From is not { } from)
            return await PlanAndLoadTask(command, user);

        var task = CreateTaskEntity(command, from);
        await SaveTasks([task]);
        _logger.LogInformation("Задача {TaskId} была добавлена для пользователя {UserId}", task.Id, user.Id);
        return Result.Ok((task.Id.ToString(), task));
    }

    private async Task<Result<(string, DomainTask)>> PlanAndLoadTask(CreateTaskCommand command, User user)
    {
        var result = await _plannerService.PlanTask(command, user);
        if (result.IsFailed)
            return Result.Fail(result.Errors);

        var task = await _dbContext.Tasks.FindAsync(command.Id);
        return task is null
            ? Result.Fail("Запланированная задача не найдена")
            : Result.Ok((task.Id.ToString(), task));
    }

    private async Task<Dictionary<Guid, TaskSyncResult>> SyncTasksForUser(List<CreateTaskCommand> commands, User user)
    {
        var ids = commands.Select(c => c.Id).ToList();
        var existingTasks = await _dbContext.Tasks.AsNoTracking()
            .Where(t => ids.Contains(t.Id)).ToDictionaryAsync(t => t.Id);
        var batch = PrepareSyncBatch(commands, existingTasks);

        await SaveTasks(batch.ManualTasks);
        foreach (var task in batch.ManualTasks)
            batch.Results[task.Id] = new TaskSyncResult(TaskSyncStatus.Created);

        await PlanTasksForSync(batch, user);
        return batch.Results;
    }

    private static SyncBatch PrepareSyncBatch(List<CreateTaskCommand> commands, Dictionary<Guid, DomainTask> existingTasks)
    {
        var batch = new SyncBatch([], [], []);
        foreach (var command in commands)
        {
            if (existingTasks.TryGetValue(command.Id, out var existing))
            {
                batch.Results[command.Id] = GetExistingTaskStatus(existing, command);
            }
            else if (command.From is { } from)
            {
                batch.ManualTasks.Add(CreateTaskEntity(command, from));
            }
            else
            {
                batch.CommandsToPlan.Add(command);
            }
        }
        return batch;
    }

    private static TaskSyncResult GetExistingTaskStatus(DomainTask task, CreateTaskCommand command) =>
        HasSameContent(task, command, command.From is null) && task.Version == 0
            ? new TaskSyncResult(TaskSyncStatus.Synced)
            : new TaskSyncResult(TaskSyncStatus.Conflicted, CreateConflictMessage);

    private async Task PlanTasksForSync(SyncBatch batch, User user)
    {
        if (batch.CommandsToPlan.Count == 0)
            return;

        var result = await _plannerService.PlanMultipleTasks(batch.CommandsToPlan, user);
        if (result.IsFailed)
        {
            var message = string.Join("; ", result.Errors.Select(e => e.Message));
            foreach (var command in batch.CommandsToPlan)
                batch.Results[command.Id] = new TaskSyncResult(TaskSyncStatus.NotPlanned, message);
            return;
        }

        foreach (var id in result.Value.PlannedTaskIds)
            batch.Results[id] = new TaskSyncResult(TaskSyncStatus.Created);
        foreach (var task in result.Value.UnplannedTasks)
            batch.Results[task.TaskId] = new TaskSyncResult(TaskSyncStatus.NotPlanned, task.Reason);
    }

    private async Task SaveTasks(List<DomainTask> tasks)
    {
        if (tasks.Count == 0)
            return;
        await _dbContext.Tasks.AddRangeAsync(tasks);
        await _dbContext.SaveChangesAsync();
    }

    private static DomainTask CreateTaskEntity(CreateTaskCommand command, DateTime from) =>
        new(command.Id, command.UserId, command.Name, command.Type, command.Duration,
            from, command.Priority, command.Deadline, command.Description);

    private sealed record SyncBatch(
        Dictionary<Guid, TaskSyncResult> Results,
        List<DomainTask> ManualTasks,
        List<CreateTaskCommand> CommandsToPlan);

    private async Task<Result> ValidateCreateTaskSync(List<CreateTaskCommand> commands)
    {
        if (commands.Count == 0)
            return Result.Fail("Список задач пуст");
        var userId = commands[0].UserId;
        var user = await _dbContext.Users.FindAsync(userId);
        if (user is null)
            return Result.Fail("Пользователь не найден");
        if (commands.Any(x => x.UserId != userId))
            return Result.Fail("У одной из задач отличается создатель");
        if (commands.Any(x => x.Id == Guid.Empty))
            return Result.Fail("Некорректный ID задачи");
        if (commands.Any(x => x.Duration <= TimeSpan.Zero))
            return Result.Fail("Длительность задачи не может быть отрицательной");

        var duplicateIds = commands
            .GroupBy(x => x.Id)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key)
            .ToList();
        if (duplicateIds.Count > 0)
            return Result.Fail(
                $"Повторяющиеся ID: {string.Join(", ", duplicateIds)}");

        return Result.Ok();
    }

    private static bool HasSameContent(DomainTask task, CreateTaskCommand command, bool ignoreStartTime = false)
    {
        return task.OwnerId == command.UserId
               && task.Name == command.Name
               && task.Description == command.Description
               && task.Type == command.Type
               && task.Duration == command.Duration
               && (task.From == command.From || ignoreStartTime)
               && task.Priority == command.Priority
               && task.Deadline == command.Deadline;
    }

    private async Task<User?> LockUserAsync(Guid userId)
    {
        var user = await _dbContext.Users
            .FromSqlInterpolated($"""SELECT * FROM "Users" WHERE "Id" = {userId} FOR UPDATE""")
            .ToListAsync();
        return user.SingleOrDefault();
    }
}
