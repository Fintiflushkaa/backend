namespace API.Dto;

internal record UserDto
{
    public UserDto()
    {
        Id = Guid.NewGuid();
    }
    public Guid Id { get; init; }
    public string Name { get; init; }
    public string Email { get; init; }
    public string Password { get; init; }
}