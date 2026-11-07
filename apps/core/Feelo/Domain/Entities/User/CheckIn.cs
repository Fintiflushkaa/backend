namespace Domain.Entities.User;

public sealed class CheckIn
{
    public Guid Id { get; private set; }
    public Guid UserId { get; private set; }

    public DateTimeOffset RecordedAt { get; private set; }

    public int? Alertness { get; private set; }
    public int? Concentration { get; private set; }
}