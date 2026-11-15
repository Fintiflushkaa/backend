namespace App.Planning;

public sealed record UnplannedTask(
    Guid TaskId,
    string Name,
    string Reason);