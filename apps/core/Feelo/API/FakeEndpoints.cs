using System.Security.Claims;
using API.Dto;
using Domain.Entities;
using Infrastructure.Services.Impl;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace API;

public static class EndpointsExtensions
{
    private static readonly User FakeUserDto = new("Антон Селяков", "1", "1",
    [
        new()
        {
            Start = TimeSpan.FromHours(0),
            End = TimeSpan.FromHours(9),
            Activity = ActivityType.Sleep
        }
    ], new SleepShift());
    
    private static readonly Dictionary<Guid, User> FakeUsers = new()
    {
        {
            FakeUserDto.Id, FakeUserDto
        }
    };

    private static readonly TaskDto FakeTaskDto =
        new(FakeUserDto.Id)
        {
            Name = "Task 1", Description = "description", Type = ActivityType.Sleep,
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
        app.MapPost("/auth/register", (UserDto newUser) =>
        {
            if (FakeUsers.Any(x => x.Value.Email == newUser.Email))
                return Results.Conflict();
            var user = new User(newUser.Name, newUser.Email, newUser.Password,new List<CircadianWindow>(), new SleepShift());
            FakeUsers[user.Id] = user;
            return Results.Created();
        })
        .Produces(StatusCodes.Status409Conflict)
        .Produces(StatusCodes.Status201Created);
        app.MapPost("/auth/login", ([FromServices] AuthTokenService authTokenService, string email, string password) =>
        {
            if (!FakeUsers.Any(x => x.Value.Email == email && x.Value.Password == password))
                return Results.NotFound();
            var userId = FakeUsers.First(x => x.Value.Email == email).Value.Id;
            return Results.Ok(authTokenService.GenerateAuthToken(userId.ToString()));
        })
        .Produces(StatusCodes.Status404NotFound)
        .Produces<string>();
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
        })
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status404NotFound)
        .Produces<List<TaskDto>>();
        app.MapPost("/tasks/create", [Authorize](HttpContext context, BaseTaskDto newTaskDto) =>
        {
            var userId = context.User.FindFirst(ClaimTypes.PrimarySid)?.Value;
            if (userId == null)
                return Results.Unauthorized();
            var newTask = new TaskDto(new Guid(userId))
            {
                Name = newTaskDto.Name,
                Description = newTaskDto.Description,
                From = DateTime.UtcNow,
                To = DateTime.UtcNow + newTaskDto.Duration,
                Duration = newTaskDto.Duration,
                Created = DateTime.UtcNow,
                Updated = DateTime.UtcNow,
            };
            FakeTasks[newTask.Id] = newTask;
            return Results.Created(newTask.Id.ToString(), newTask);
        })
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces<List<TaskDto>>(StatusCodes.Status201Created);
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
        })
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status404NotFound)
        .Produces(StatusCodes.Status403Forbidden)
        .Produces(StatusCodes.Status200OK);
        app.MapGet("/circadian", [Authorize](HttpContext context) =>
        {
            var userId = context.User.FindFirst(ClaimTypes.PrimarySid)?.Value;
            if (userId == null)
                return Results.Unauthorized();
            return Results.Ok(FakeUsers[Guid.Parse(userId)].CircadianWindows);
        })
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces<List<CircadianWindowDto>>();
        app.MapPost("/survey/submit", [Authorize](HttpContext context, SurveyAnswerDto surveyAnswerDto) =>
        {
            var userId = context.User.FindFirst(ClaimTypes.PrimarySid)?.Value;
            if (userId == null)
                return Results.Unauthorized();
            var user = FakeUsers[Guid.Parse(userId)];
            
            var circadianWindows = new List<CircadianWindow>
            {
                new()
                {
                    Activity = ActivityType.Sleep, End = surveyAnswerDto.WakeTime, Start = surveyAnswerDto.SleepTime
                }
            };
            foreach (var window in surveyAnswerDto.PeakPeriods)
                circadianWindows.Add(new CircadianWindow
                {
                    Activity = window.Activity, Start = window.Start, End = window.End
                });

            var sleepShift = new SleepShift
            {
                SleepTimeShift = surveyAnswerDto.FreeDaySleepTime - surveyAnswerDto.SleepTime,
                WakeTimeShift = surveyAnswerDto.FreeDayWakeTime - surveyAnswerDto.WakeTime
            };
            FakeUsers[Guid.Parse(userId)] = user with
            {
                CircadianWindows = circadianWindows, SleepShift = sleepShift
            };
            return Results.Ok();
        })
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status200OK);
    }
}