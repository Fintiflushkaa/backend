namespace App.Commands;

public sealed record RegisterCommand(
    string Name,
    string Email,
    string Password);