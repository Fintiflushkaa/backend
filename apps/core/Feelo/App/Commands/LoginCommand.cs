namespace App.Commands;

public sealed record LoginCommand(
    string Email,
    string Password);