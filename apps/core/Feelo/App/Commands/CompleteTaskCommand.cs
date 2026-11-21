namespace App.Commands;

public sealed record CompleteTaskCommand(Guid Id,
    Guid UserId,
    DateTime CompletedAt);