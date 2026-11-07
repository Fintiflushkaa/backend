using API.Dto;
using API.Dto.Task;
using App.Commands;
using Riok.Mapperly.Abstractions;
using Task = Domain.Entities.Task.Task;
using TaskDto = API.Dto.Task.TaskDto;

namespace API.Mappings;

[Mapper]
public partial class ApiMapper
{
    public partial RegisterCommand Map(Dto.User.UserDto dto);
    public partial SubmitSurveyCommand Map(SurveyAnswerDto dto, Guid userId);
    public partial CreateTaskCommand Map(BaseTaskDto dto, Guid userId);
    public partial List<TaskDto> Map(List<Task> tasks);
    public partial TaskDto Map(Task task);
    public UpdateTaskCommand Map(UpdateTaskDto dto, Guid userId, Guid taskId) =>
        new(userId, taskId, dto.Name, dto.Type, dto.Duration, dto.From,
            dto.Description, dto.DescriptionSpecified);
}
