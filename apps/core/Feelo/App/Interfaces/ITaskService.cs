using App.Commands;
using App.Synchronization;
using FluentResults;
using DomainTask = Domain.Entities.Task.Task;

namespace App.Interfaces;

public interface ITaskService
{
    public Task<Result<(string, DomainTask)>> CreateTask(CreateTaskCommand command);
    public Task<Result<Dictionary<Guid, TaskSyncResult>>> SyncTasks(List<CreateTaskCommand> commands);
    public Task<Result<List<DomainTask>>> GetAllTasks(GetAllTasksCommand command);
    public Task<Result> UpdateTask(UpdateTaskCommand command);
    public Task<Result> DeleteTask(DeleteTaskCommand command);
}