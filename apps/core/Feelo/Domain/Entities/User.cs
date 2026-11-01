namespace Domain.Entities;

public sealed record User(string Name,
    string Email,
    string Password,
    List<CircadianWindow> CircadianWindows,
    SleepShift SleepShift)
{
    public Guid Id { get; init; } = Guid.NewGuid();
}