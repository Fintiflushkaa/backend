namespace App.Commands;

public record DeleteTaskCommand(Guid TaskId, Guid UserId);