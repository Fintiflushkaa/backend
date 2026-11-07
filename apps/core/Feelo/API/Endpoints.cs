using System.Security.Claims;
using API.Dto;
using API.Dto.Circadian;
using API.Dto.Task;
using API.Dto.User;
using API.Mappings;
using App.Commands;
using App.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace API;

public static class Endpoints
{
    public static void MapEndpoints(this WebApplication app)
    {
        app.MapPost("/auth/register", async (UserDto newUser, IAuthService authService, ApiMapper mapper) =>
            {
                var command = mapper.Map(newUser);
                var result = await authService.Register(command);
                if (result.IsSuccess)
                    return Results.Created();
                return Results.Conflict(result.Errors.First().Message);
            })
            .Produces(StatusCodes.Status409Conflict)
            .Produces(StatusCodes.Status201Created);
        app.MapPost("/auth/login", async (string email, string password, IAuthService authService) =>
            {
                var command = new LoginCommand(email, password);
                var result = await authService.Login(command);
                if (result.IsSuccess)
                    return Results.Ok(result.Value);
                return Results.NotFound(result.Errors.First().Message);
            })
            .Produces(StatusCodes.Status404NotFound)
            .Produces<string>();
        app.MapPost("/survey/submit", [Authorize] async (HttpContext context,
                SurveyAnswerDto request,
                ISurveyService surveyService,
                ApiMapper mapper) =>
        {
            var userId = context.User.FindFirst(ClaimTypes.PrimarySid)?.Value;
            if (userId == null)
                return Results.Unauthorized();

            var command = mapper.Map(request, new Guid(userId));

            var result = await surveyService.SubmitSurvey(command);
            
            if (result.IsSuccess)
                return Results.Ok();
            return Results.BadRequest(result.Errors);
        })
        .Produces(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status200OK);
        app.MapPost("/tasks/create", [Authorize] async (HttpContext context,
                BaseTaskDto newTaskDto,
                ITaskService taskService,
                ApiMapper mapper) =>
        {
            var userId = context.User.FindFirst(ClaimTypes.PrimarySid)?.Value;
            if (userId == null)
                return Results.Unauthorized();

            var command = mapper.Map(newTaskDto, new Guid(userId));
            
            var result = await taskService.CreateTask(command);

            if (result.IsSuccess)
            {
                var task = mapper.Map(result.Value.Item2);
                return Results.Created($"/tasks/{task.Id}", task);
            }
            return Results.BadRequest(result.Errors);
        })
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status400BadRequest)
        .Produces<TaskDto>(StatusCodes.Status201Created);
        app.MapGet("/tasks/", [Authorize] async (HttpContext context,
                ITaskService taskService,
                ApiMapper mapper) =>
            {
                var userId = context.User.FindFirst(ClaimTypes.PrimarySid)?.Value;
                if (userId == null)
                    return Results.Unauthorized();

                var command = new GetAllTasksCommand(new Guid(userId));

                var result = await taskService.GetAllTasks(command);
                if (result.IsSuccess)
                {
                    var tasks = mapper.Map(result.Value);
                    return Results.Ok(tasks);
                }

                return Results.BadRequest(result.Errors);
            })
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces<List<TaskDto>>();
        
        app.MapPut("/tasks/{taskId:guid}/", [Authorize] async (HttpContext context,
                UpdateTaskDto request,
                Guid taskId,
                ITaskService taskService,
                ApiMapper mapper) =>
            {
                var userId = context.User.FindFirst(ClaimTypes.PrimarySid)?.Value;
                if (userId == null)
                    return Results.Unauthorized();
                
                var command = mapper.Map(request, new Guid(userId), taskId);

                var result = await taskService.UpdateTask(command);
                if (result.IsSuccess)
                    return Results.Ok();
                return Results.BadRequest(result.Errors);
            })
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status200OK);
        app.MapDelete("/tasks/{taskId:guid}/", [Authorize] async (HttpContext context,
                Guid taskId,
                ITaskService taskService,
                ApiMapper mapper) =>
            {
                var userId = context.User.FindFirst(ClaimTypes.PrimarySid)?.Value;
                if (userId == null)
                    return Results.Unauthorized();
                
                var command = new DeleteTaskCommand(taskId, new Guid(userId));

                var result = await taskService.DeleteTask(command);
                if (result.IsSuccess)
                    return Results.Ok();
                return Results.BadRequest(result.Errors);
            })
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status200OK);
        app.MapGet("/circadian", [Authorize] async (HttpContext context,
                [FromQuery] DateOnly? date,
                ICircadianService circadianService) =>
            {
                var userId = context.User.FindFirst(ClaimTypes.PrimarySid)?.Value;
                if (userId == null)
                    return Results.Unauthorized();
                
                date ??= DateOnly.FromDateTime(DateTime.Now);

                var result = await circadianService.GetCircadianCurves(
                    new GetCircadianCommand(new Guid(userId), date));

                return result.IsSuccess
                    ? Results.Ok(result.Value)
                    : Results.BadRequest(result.Errors);
            })
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces<List<CircadianCurveDto>>();
    }
}
