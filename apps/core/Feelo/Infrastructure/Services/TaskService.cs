using App.Commands;
using App.Interfaces;
using App.Synchronization;
using Domain.Entities.User;
using FluentResults;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using DomainTask = Domain.Entities.Task.Task;

namespace Infrastructure.Services;

public class TaskService(ILogger<TaskService> logger, DBContext dbContext, IPlannerService plannerService)
    : ITaskService
{
    private const string CreateConflictMessage = "Задача с таким id уже существует";

    public async Task<Result<(string, DomainTask)>> CreateTask(CreateTaskCommand command)
    {
        if (command.Duration < TimeSpan.Zero)
            return Result.Fail("Длительность задачи не может быть отрицательной");
        if (command.Date == default)
            return Result.Fail("Укажите дату задачи");

        await using var transaction = await dbContext.Database.BeginTransactionAsync();
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

        await using var transaction = await dbContext.Database.BeginTransactionAsync();
        var user = await LockUserAsync(commands[0].UserId);
        if (user is null)
            return Result.Fail("Пользователь не найден");

        var result = await SyncTasksForUser(commands, user);
        await transaction.CommitAsync();
        return Result.Ok(result);
    }

    public async Task<Result<List<DomainTask>>> GetAllTasks(GetAllTasksCommand command)
    {
        var user = await dbContext.Users.FindAsync(command.UserId);
        if (user == null)
            return Result.Fail("Пользователь не найден");
        var tasks = await dbContext.Tasks
            .Where(x => x.OwnerId == command.UserId)
            .ToListAsync();
        logger.LogInformation("Получены все задачи пользователя {UserId} в количестве {Count}", user.Id, tasks.Count);
        return tasks;
    }

    public async Task<Result<Dictionary<Guid, TaskSyncResult>>> UpdateTask(UpdateTaskCommand command)
    {
        var validation = await ValidateUpdateTaskSync(command);
        if (validation.IsFailed)
            return Result.Fail(validation.Errors);

        await using var transaction = await dbContext.Database.BeginTransactionAsync();
        var user = await LockUserAsync(command.UserId);
        if (user is null)
            return Result.Fail("Пользователь не найден");

        var task = await FindOwnedTask(command.TaskId, command.UserId);
        if (task is null)
            return Result.Fail("Задача не найдена");

        if (HasSameContent(task, command))
            return Result.Ok(new Dictionary<Guid, TaskSyncResult>
            {
                { task.Id, new TaskSyncResult(task.Version, TaskSyncStatus.Synced) }
            });
        if (task.Version != command.Version)
            return Result.Ok(new Dictionary<Guid, TaskSyncResult>()
            {
                { task.Id, new TaskSyncResult(task.Version, TaskSyncStatus.Conflicted, task) }
            });

        var from = command.From;
        if (command.Date.HasValue && from is null)
        {
            var planning = await FindUpdatedTaskStart(command, user, task);
            if (planning.IsFailed)
                return Result.Fail(planning.Errors);
            from = planning.Value;
        }

        task.UpdateTask(command.Name, command.Type, command.Duration, from, command.Priority,
            command.Deadline, command.IsCompleted, command.CompletedAt, command.Description, command.DescriptionSpecified,
            command.Date);
        await dbContext.SaveChangesAsync();
        logger.LogInformation("Задача {TaskId} была обновлена для пользователя {UserId}", task.Id, user.Id);
        await transaction.CommitAsync();
        return Result.Ok(new Dictionary<Guid, TaskSyncResult>
        {
            {task.Id, new TaskSyncResult(task.Version, TaskSyncStatus.Modified)}
        });
    }

    public async Task<Result> DeleteTask(DeleteTaskCommand command)
    {
        await using var transaction = await dbContext.Database.BeginTransactionAsync();
        var user = await LockUserAsync(command.UserId);
        if (user is null)
            return Result.Fail("Пользователь не найден");

        var task = await FindOwnedTask(command.TaskId, command.UserId);
        if (task is null)
            return Result.Fail("Задача не найдена");

        dbContext.Tasks.Remove(task);
        await dbContext.SaveChangesAsync();
        await transaction.CommitAsync();
        return Result.Ok();
    }

    private Task<DomainTask?> FindOwnedTask(Guid taskId, Guid userId) =>
        dbContext.Tasks.FirstOrDefaultAsync(t => t.Id == taskId && t.OwnerId == userId);

    private Task<Result<DateTime>> FindUpdatedTaskStart(UpdateTaskCommand command, User user, DomainTask task)
    {
        var description = command.DescriptionSpecified || command.Description is not null
            ? command.Description : task.Description;
        var planningCommand = new CreateTaskCommand(task.Id, task.OwnerId,
            command.Name ?? task.Name, description, command.Type ?? task.Type,
            command.Date!.Value, null, command.Duration ?? task.Duration,
            command.Priority ?? task.Priority, command.Deadline ?? task.Deadline);
        return plannerService.FindStartForTask(planningCommand, user);
    }

    private async Task<Result<(string, DomainTask)>> CreateTaskForUser(CreateTaskCommand command, User user)
    {
        if (await dbContext.Tasks.AnyAsync(t => t.Id == command.Id))
            return Result.Fail($"Задача с id {command.Id} уже существует!");

        if (command.From is not { } from)
            return await PlanAndLoadTask(command, user);

        var task = CreateTaskEntity(command, from);
        await SaveTasks([task]);
        logger.LogInformation("Задача {TaskId} была добавлена для пользователя {UserId}", task.Id, user.Id);
        return Result.Ok((task.Id.ToString(), task));
    }

    private async Task<Result<(string, DomainTask)>> PlanAndLoadTask(CreateTaskCommand command, User user)
    {
        var result = await plannerService.PlanTask(command, user);
        if (result.IsFailed)
            return Result.Fail(result.Errors);

        var task = await dbContext.Tasks.FindAsync(command.Id);
        return task is null
            ? Result.Fail("Запланированная задача не найдена")
            : Result.Ok((task.Id.ToString(), task));
    }

    private async Task<Dictionary<Guid, TaskSyncResult>> SyncTasksForUser(List<CreateTaskCommand> commands, User user)
    {
        var ids = commands.Select(c => c.Id).ToList();
        var existingTasks = await dbContext.Tasks.AsNoTracking()
            .Where(t => ids.Contains(t.Id)).ToDictionaryAsync(t => t.Id);
        var batch = PrepareSyncBatch(commands, existingTasks);

        await SaveTasks(batch.ManualTasks);
        foreach (var task in batch.ManualTasks)
            batch.Results[task.Id] = new TaskSyncResult(task.Version, TaskSyncStatus.Created);

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
            ? new TaskSyncResult(task.Version, TaskSyncStatus.Synced)
            : new TaskSyncResult(task.Version, TaskSyncStatus.Conflicted, null, CreateConflictMessage);

    private async Task PlanTasksForSync(SyncBatch batch, User user)
    {
        if (batch.CommandsToPlan.Count == 0)
            return;

        var result = await plannerService.PlanMultipleTasks(batch.CommandsToPlan, user);
        if (result.IsFailed)
        {
            var message = string.Join("; ", result.Errors.Select(e => e.Message));
            foreach (var command in batch.CommandsToPlan)
                batch.Results[command.Id] = new TaskSyncResult(null, TaskSyncStatus.NotPlanned, null, message);
            return;
        }

        foreach (var task in result.Value.PlannedTasks)
            batch.Results[task.Id] = new TaskSyncResult(task.Version, TaskSyncStatus.Created);
        foreach (var task in result.Value.UnplannedTasks)
            batch.Results[task.TaskId] = new TaskSyncResult(null, TaskSyncStatus.NotPlanned, null, task.Reason);
    }

    private async Task SaveTasks(List<DomainTask> tasks)
    {
        if (tasks.Count == 0)
            return;
        await dbContext.Tasks.AddRangeAsync(tasks);
        await dbContext.SaveChangesAsync();
    }

    private static DomainTask CreateTaskEntity(CreateTaskCommand command, DateTime from) =>
        new(command.Id, command.UserId, command.Name, command.Type, command.Duration,
            from, command.Priority, command.Date, command.Deadline, command.Description);

    private sealed record SyncBatch(
        Dictionary<Guid, TaskSyncResult> Results,
        List<DomainTask> ManualTasks,
        List<CreateTaskCommand> CommandsToPlan);

    private async Task<Result> ValidateCreateTaskSync(List<CreateTaskCommand> commands)
    {
        if (commands.Count == 0)
            return Result.Fail("Список задач пуст");
        var userId = commands[0].UserId;
        var user = await dbContext.Users.FindAsync(userId);
        if (user is null)
            return Result.Fail("Пользователь не найден");
        if (commands.Any(x => x.UserId != userId))
            return Result.Fail("У одной из задач отличается создатель");
        if (commands.Any(x => x.Id == Guid.Empty))
            return Result.Fail("Некорректный ID задачи");
        if (commands.Any(x => x.Date == default))
            return Result.Fail("Укажите дату задачи");
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

    private async Task<Result> ValidateUpdateTaskSync(UpdateTaskCommand command)
    {
        var userId = command.UserId;
        var user = await dbContext.Users.FindAsync(userId);
        if (user is null)
            return Result.Fail("Пользователь не найден");
        if (command.UserId != userId)
            return Result.Fail("У одной из задач отличается создатель");
        if (command.TaskId == Guid.Empty)
            return Result.Fail("Некорректный ID задачи");
        if (command.Date == default(DateOnly))
            return Result.Fail("Укажите корректную дату задачи");
        if (command.Duration <= TimeSpan.Zero)
            return Result.Fail("Длительность задачи не может быть отрицательной");

        return Result.Ok();
    }

    private static bool HasSameContent(DomainTask task, CreateTaskCommand command, bool ignoreStartTime = false)
    {
        return task.OwnerId == command.UserId
               && task.Name == command.Name
               && task.Description == command.Description
               && task.Type == command.Type
               && task.Duration == command.Duration
               && task.Date == command.Date
               && (task.From == command.From || ignoreStartTime)
               && task.Priority == command.Priority
               && task.Deadline == command.Deadline;
    }

    private static bool HasSameContent(DomainTask task, UpdateTaskCommand command)
    {
        var description = command.DescriptionSpecified ||
                          command.Description is not null
            ? command.Description
            : task.Description;
        var completedAt = command.IsCompleted == false
            ? null
            : command.CompletedAt ?? task.CompletedAt;

        return task.OwnerId == command.UserId
               && task.Name == (command.Name ?? task.Name)
               && task.Description == description
               && task.Type == (command.Type ?? task.Type)
               && task.Duration == (command.Duration ?? task.Duration)
               && task.Date == (command.Date ?? task.Date)
               && task.From == (command.From ?? task.From)
               && task.Priority == (command.Priority ?? task.Priority)
               && task.Deadline == (command.Deadline ?? task.Deadline)
               && task.IsCompleted == (command.IsCompleted ?? task.IsCompleted)
               && task.CompletedAt == completedAt;
    }

    protected virtual async Task<User?> LockUserAsync(Guid userId)
    {
        var user = await dbContext.Users
            .FromSqlInterpolated($"""SELECT * FROM "Users" WHERE "Id" = {userId} FOR UPDATE""")
            .ToListAsync();
        return user.SingleOrDefault();
    }
}
