using API.Dto;
using API.Dto.Task;
using API.Dto.User;
using App.Commands;
using App.Synchronization;
using Domain.Entities.User;
using Riok.Mapperly.Abstractions;
using Task = Domain.Entities.Task.Task;
using TaskDto = API.Dto.Task.TaskDto;

namespace API.Mappings;

[Mapper]
public partial class ApiMapper
{
    public partial RegisterCommand Map(UserDto dto);
    public partial SubmitSurveyCommand Map(SurveyAnswerDto dto, Guid userId);
    [MapperIgnoreSource(nameof(TaskDto.OwnerId))]
    [MapperIgnoreSource(nameof(TaskDto.Created))]
    [MapperIgnoreSource(nameof(TaskDto.Updated))]
    [MapperIgnoreSource(nameof(TaskDto.To))]
    [MapperIgnoreSource(nameof(TaskDto.Version))]
    [MapperIgnoreSource(nameof(TaskDto.IsCompleted))]
    [MapperIgnoreSource(nameof(TaskDto.CompletedAt))]
    public partial CreateTaskCommand Map(TaskDto dto, Guid userId);
    public partial List<TaskDto> Map(List<Task> tasks);
    public partial TaskDto Map(Task task);
    public partial Dictionary<Guid, TaskSyncResult<TaskDto>> Map(Dictionary<Guid, TaskSyncResult> results);
    public UpdateTaskCommand Map(UpdateTaskDto dto, Guid userId, Guid taskId) =>
        new(userId, taskId, dto.Version, dto.Name, dto.Type, dto.Duration, dto.From, dto.Priority, dto.Deadline,
            dto.IsCompleted, dto.CompletedAt, dto.Description, dto.DescriptionSpecified, dto.Date);
    
    [MapperIgnoreSource(nameof(User.PasswordHash))]
    [MapperIgnoreSource(nameof(User.Email))]
    public partial ProfileDto Map(User user);
}
