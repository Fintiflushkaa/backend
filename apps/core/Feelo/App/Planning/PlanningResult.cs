namespace App.Planning;

public sealed record PlanningResult(
    IReadOnlyList<Guid> PlannedTaskIds,
    IReadOnlyList<UnplannedTask> UnplannedTasks);