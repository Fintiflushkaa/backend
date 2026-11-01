using Domain.Entities;

namespace API.Dto;

internal record UpdateTaskDto : BaseTaskDto
{
    public DateTime From { get; set; }
    public DateTime To { get; set; }
}