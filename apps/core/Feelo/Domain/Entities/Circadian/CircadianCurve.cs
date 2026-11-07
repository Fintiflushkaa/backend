namespace Domain.Entities.Circadian;

public sealed record CircadianCurve(
    EnergyCurveKind Kind,
    IReadOnlyList<EnergyPoint> Points,
    IReadOnlyList<CircadianWindow> Windows);