using App.Commands;
using App.Interfaces;
using FluentResults;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using DomainTask = Domain.Entities.Task.Task;

namespace Infrastructure.Services;

public class TaskService : ITaskService
{
    private readonly ILogger<TaskService> _logger;
    private readonly DBContext _dbContext;
    
    public TaskService(ILogger<TaskService> logger, DBContext dbContext)
    {
        _logger = logger;
        _dbContext = dbContext;
    }
    
    public async Task<Result<(string, DomainTask)>> CreateTask(CreateTaskCommand command)
    {
        if (command.Duration < TimeSpan.Zero)
            return Result.Fail("Длительность задачи не может быть отрицательной");
        var user = await _dbContext.Users.FindAsync(command.UserId);
        if (user == null)
            return Result.Fail("Пользователь не найден");
        var newTask = new DomainTask(command.UserId, command.Name, command.Type,
            command.Duration, command.From, command.Description);
        await _dbContext.Tasks.AddAsync(newTask);
        await _dbContext.SaveChangesAsync();
        _logger.LogInformation("Задача {TaskId} была добавлена для пользователя {UserId}", newTask.Id, user.Id);
        return Result.Ok((newTask.Id.ToString(), newTask));
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
        var user = await _dbContext.Users.FindAsync(command.UserId);
        if (user == null)
            return Result.Fail("Пользователь не найден");
        var task = await _dbContext.Tasks
            .FirstOrDefaultAsync(t =>
                t.Id == command.TaskId &&
                t.OwnerId == command.UserId);
        if (task == null)
            return Result.Fail("Задача не найдена");
        task.UpdateTask(command.Name, command.Type, command.Duration, command.From,
            command.Description, command.DescriptionSpecified);
        await _dbContext.SaveChangesAsync();
        _logger.LogInformation("Задача {TaskId} была обновлена для пользователя {UserId}", task.Id, user.Id);
        return Result.Ok();
    }

    public async Task<Result> DeleteTask(DeleteTaskCommand command)
    {
        var user = await _dbContext.Users.FindAsync(command.UserId);
        if (user == null)
            return Result.Fail("Пользователь не найден");
        var task = await _dbContext.Tasks
            .FirstOrDefaultAsync(t =>
                t.Id == command.TaskId &&
                t.OwnerId == command.UserId);
        if (task == null)
            return Result.Fail("Задача не найдена");
        _dbContext.Tasks.Remove(task);
        await _dbContext.SaveChangesAsync();
        return Result.Ok();
    }
}
