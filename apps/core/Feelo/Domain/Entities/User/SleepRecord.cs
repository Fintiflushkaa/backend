namespace Domain.Entities.User;

public sealed class SleepRecord
{
    public Guid Id { get; private set; }
    public Guid UserId { get; private set; }

    public DateTimeOffset FellAsleepAt { get; private set; }
    public DateTimeOffset WokeUpAt { get; private set; }

    public int? Quality { get; private set; }

    public TimeSpan Duration => WokeUpAt - FellAsleepAt;
}