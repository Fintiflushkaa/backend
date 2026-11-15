using Domain.Entities.Circadian;

namespace Domain.Entities.User;

public sealed record BestActivityWindow(
    EnergyCurveKind Curve,
    DateTimeOffset Start,
    DateTimeOffset End,
    double AverageEnergy);