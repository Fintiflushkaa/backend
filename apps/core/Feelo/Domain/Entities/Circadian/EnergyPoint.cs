namespace Domain.Entities.Circadian;

public sealed record EnergyPoint(
    DateTimeOffset Time,
    double Energy);