using API.Dto.Task;

namespace API.Dto.Circadian;

public sealed record CircadianCurveDto(
    EnergyCurveKind Kind,
    IReadOnlyList<EnergyPointDto> Points,
    IReadOnlyList<CircadianWindowDto> Windows);