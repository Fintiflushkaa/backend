using System.Security.Claims;
using API.Dto;
using Infrastructure.Services.Impl;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace API;

public static class EndpointsExtensions
{
    private static readonly UserDto FakeUserDto = new()
    {
        Name = "Антон Селяков", Email = "1", Password = "1",
        CircadianWindows =
        [
            new()
            {
                Start = TimeSpan.FromHours(0),
                End = TimeSpan.FromHours(9),
                Activity = ActivityType.Sleep
            }
        ]
    };
    
    private static readonly Dictionary<Guid, UserDto> FakeUsers = new()
    {
        {
            FakeUserDto.Id, FakeUserDto
        }
    };

    private static readonly TaskDto FakeTaskDto =
        new( "Task 1", "samdfkdjskda", ActivityType.Sleep, TimeSpan.FromHours(1), FakeUserDto.Id)
        {
            Created = DateTime.UtcNow, Updated = DateTime.UtcNow
        };

    private static readonly Dictionary<Guid, TaskDto> FakeTasks = new()
    {
        {
            FakeTaskDto.Id, FakeTaskDto
        }
    };
    
    public static void MapFakeEndpoints(this WebApplication app)
    {
        app.MapPost("/auth/register", (BaseUserDto newUser) =>
        {
            if (FakeUsers.Any(x => x.Value.Email == newUser.Email))
                return Results.Conflict();
            var user = new UserDto()
            {
                Email = newUser.Email,
                Password = newUser.Password,
                Name = newUser.Name
            };
            FakeUsers[user.Id] = user;
            return Results.Ok();
        });
        app.MapPost("/auth/login", ([FromServices] AuthTokenService authTokenService, string email, string password) =>
        {
            if (!FakeUsers.Any(x => x.Value.Email == email && x.Value.Password == password))
                return Results.NotFound();
            var userId =FakeUsers.First(x => x.Value.Email == email).Value.Id;
            return Results.Ok(authTokenService.GenerateAuthToken(userId.ToString()));
        });
        app.MapGet("/tasks", [Authorize](HttpContext context) =>
        {
            var userId = context.User.FindFirst(ClaimTypes.PrimarySid)?.Value;
            if (userId == null)
                return Results.Unauthorized();
            var tasks = FakeTasks
                .Where(x => x.Value.OwnerId.ToString() == userId)
                .Select(x => x.Value)
                .ToList();
            if (tasks.Count == 0)
                return Results.NotFound();
            return Results.Ok(tasks);
        });
        app.MapPost("/tasks/create", [Authorize](HttpContext context, BaseTaskDto newTaskDto) =>
        {
            var userId = context.User.FindFirst(ClaimTypes.PrimarySid)?.Value;
            if (userId == null)
                return Results.Unauthorized();
            var newTask = new TaskDto(newTaskDto.Name, newTaskDto.Description, newTaskDto.Type, newTaskDto.Duration, new Guid(userId))
            {
                From = DateTime.UtcNow,
                To = DateTime.UtcNow + newTaskDto.Duration,
                Created = DateTime.UtcNow,
                Updated = DateTime.UtcNow,
            };
            FakeTasks[newTask.Id] = newTask;
            return Results.Created(newTask.Id.ToString(), newTask);
        });
        app.MapPut("/tasks/{taskId:guid}/", [Authorize](HttpContext context, UpdateTaskDto request, Guid taskId) =>
        {
            var userId = context.User.FindFirst(ClaimTypes.PrimarySid)?.Value;
            if (userId == null)
                return Results.Unauthorized();
            if (!FakeTasks.TryGetValue(taskId, out var task))
                return Results.NotFound();
            if (task.OwnerId.ToString() != userId)
                return Results.Forbid();
            
            task = task with
            {
                Name = request.Name,
                Description = request.Description,
                Type = request.Type,
                Duration = request.Duration,
                From = request.From,
                To = request.To,
                Updated = DateTime.UtcNow
            };
            FakeTasks[task.Id] = task;
            return Results.Ok();
        });
        app.MapGet("/circadian", [Authorize](HttpContext context) =>
        {
            var userId = context.User.FindFirst(ClaimTypes.PrimarySid)?.Value;
            if (userId == null)
                return Results.Unauthorized();
            return Results.Ok(FakeUsers[Guid.Parse(userId)].CircadianWindows);
        });
        app.MapPost("/survey/submit", [Authorize](HttpContext context, SurveyAnswerDto surveyAnswerDto) =>
        {
            var userId = context.User.FindFirst(ClaimTypes.PrimarySid)?.Value;
            if (userId == null)
                return Results.Unauthorized();
            var user = FakeUsers[Guid.Parse(userId)];
            
            var circadianWindows = new List<CircadianWindowDto>
            {
                new()
                {
                    Activity = ActivityType.Sleep, End = surveyAnswerDto.WakeTime, Start = surveyAnswerDto.SleepTime
                }
            };
            foreach (var window in surveyAnswerDto.PeakPeriods)
                circadianWindows.Add(window);

            var sleepShift = new SleepShiftDto
            {
                SleepShift = surveyAnswerDto.FreeDaySleepTime - surveyAnswerDto.SleepTime,
                WakeShift = surveyAnswerDto.FreeDayWakeTime - surveyAnswerDto.WakeTime
            };
            FakeUsers[Guid.Parse(userId)] = user with
            {
                CircadianWindows = circadianWindows, SleepShift = sleepShift
            };
            return Results.Ok();
        });
    }
}