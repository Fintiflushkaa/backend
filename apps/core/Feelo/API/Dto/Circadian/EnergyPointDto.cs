namespace API.Dto.Circadian;

public sealed record EnergyPointDto(
    DateTimeOffset Time,
    double Energy);