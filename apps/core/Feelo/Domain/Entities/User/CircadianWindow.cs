using Domain.Entities.Task;

namespace Domain.Entities.User;

public sealed record CircadianWindow(
    EnergyCurveKind Curve,
    DateTimeOffset Start,
    DateTimeOffset End,
    EnergyZone Zone);

public enum EnergyZone
{
    Low,
    Normal,
    Peak
}