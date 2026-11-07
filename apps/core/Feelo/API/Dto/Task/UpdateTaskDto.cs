using Domain.Entities.Task;
using System.Text.Json.Serialization;

namespace API.Dto.Task;

public sealed record UpdateTaskDto
{
    private string? _description;

    public string? Name { get; init; }
    public string? Description
    {
        get => _description;
        init
        {
            _description = value;
            DescriptionSpecified = true;
        }
    }
    [JsonIgnore]
    public bool DescriptionSpecified { get; private init; }
    public TaskCategory? Type { get; init; }
    public DateTime? From { get; init; }
    public TimeSpan? Duration { get; init; }
}
