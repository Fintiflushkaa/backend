namespace API.Dto.Circadian;

public sealed record CircadianDto(
    DateOnly Date,
    IReadOnlyList<CircadianCurveDto> Curve);