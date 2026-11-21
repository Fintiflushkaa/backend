using App.Commands;
using App.Planning;
using Domain.Entities.User;
using FluentResults;

namespace App.Interfaces;

public interface IPlannerService
{
    public Task<Result<PlanningResult>> PlanTask(CreateTaskCommand command, User user);
    
    public Task<Result<PlanningResult>> PlanMultipleTasks(List<CreateTaskCommand> commands, User user);

    public Task<Result<DateTime>> FindStartForTask(CreateTaskCommand command, User user);
}
