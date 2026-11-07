namespace App.Commands;

public sealed record GetCircadianCommand(Guid UserId, DateOnly? Date);