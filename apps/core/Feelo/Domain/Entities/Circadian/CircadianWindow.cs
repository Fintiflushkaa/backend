using Domain.Entities.User;

namespace Domain.Entities.Circadian;

public sealed record CircadianWindow(
    DateTimeOffset Start,
    DateTimeOffset End,
    EnergyZone Zone);