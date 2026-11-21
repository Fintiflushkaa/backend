namespace App.Planning;
using DomainTask = Domain.Entities.Task.Task;

public sealed record PlanningResult(
    IReadOnlyList<DomainTask> PlannedTasks,
    IReadOnlyList<UnplannedTask> UnplannedTasks);