namespace API.Dto;

internal sealed record UserDto : BaseUserDto
{
    public List<CircadianWindowDto> CircadianWindows { get; init; }
}