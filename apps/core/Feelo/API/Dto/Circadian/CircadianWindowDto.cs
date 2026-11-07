using API.Dto.User;

namespace API.Dto.Circadian;

public sealed record CircadianWindowDto(
    DateTimeOffset Start,
    DateTimeOffset End,
    EnergyZone Zone);