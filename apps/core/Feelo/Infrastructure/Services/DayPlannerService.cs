using App.Commands;
using App.Interfaces;
using App.Planning;
using Domain.Entities.Circadian;
using Domain.Entities.Task;
using Domain.Entities.User;
using FluentResults;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using static System.Array;
using DomainTask = Domain.Entities.Task.Task;
using Task = System.Threading.Tasks.Task;

namespace Infrastructure.Services;

public class DayPlannerService : IPlannerService
{
    private readonly ILogger<DayPlannerService> _logger;
    private readonly DBContext _dbContext;

    private static readonly TimeSpan Gap = TimeSpan.FromMinutes(15);
    private static readonly TimeSpan Step = TimeSpan.FromMinutes(15);

    public DayPlannerService(ILogger<DayPlannerService> logger, DBContext dbContext)
    {
        _logger = logger;
        _dbContext = dbContext;
    }

    public async Task<Result<PlanningResult>> PlanTask(CreateTaskCommand command, User user)
    {
        var result = await PlanMultipleTasks([command], user);

        if (result.IsFailed)
            return Result.Fail(result.Errors);

        if (result.Value.UnplannedTasks.Count > 0)
            return Result.Fail(result.Value.UnplannedTasks[0].Reason);

        return Result.Ok(result.Value);
    }

    public async Task<Result<PlanningResult>> PlanMultipleTasks(List<CreateTaskCommand> commands, User user)
    {
        if (commands.Count == 0)
            return Result.Ok(new PlanningResult([], []));

        var validation = ValidatePlanningRequest(commands, user);
        if (validation.IsFailed)
            return Result.Fail(validation.Errors);

        var now = DateTime.UtcNow;
        var (validCommands, unplannedTasks) = ValidateCommands(commands, now);
        if (validCommands.Count == 0)
            return Result.Ok(new PlanningResult(Empty<Guid>(), unplannedTasks));

        var day = await LoadPlanningDay(user, now);
        var newTasks = PlaceTasks(validCommands, day, unplannedTasks);

        await SavePlannedTasks(newTasks);
        _logger.LogInformation("Планирование пользователя {UserId}: размещено {Planned}, не размещено {Unplanned}",
            user.Id, newTasks.Count, unplannedTasks.Count);
        return Result.Ok(new PlanningResult(newTasks.Select(t => t.Id).ToList(), unplannedTasks));
    }

    private async Task<PlanningDay> LoadPlanningDay(User user, DateTime now)
    {
        var date = DateOnly.FromDateTime(now);
        var curves = CircadianCalculator.Calculate(user.SleepProfile!, user.RhythmPreferences!, date,
            TimeZoneInfo.Utc).ToDictionary(c => c.Kind);
        var (start, end) = GetDayBounds(user.SleepProfile!, date);
        var plannedTasks = await LoadPlannedTasks(user.Id, start, end);
        return new PlanningDay(now, start, end, curves, plannedTasks);
    }

    private sealed record PlanningDay(
        DateTime Now,
        DateTime Start,
        DateTime End,
        Dictionary<EnergyCurveKind, CircadianCurve> Curves,
        List<DomainTask> PlannedTasks);

    private static Result ValidatePlanningRequest(List<CreateTaskCommand> commands, User user)
    {
        if (user.SleepProfile is null || user.RhythmPreferences is null)
            return Result.Fail("Сначала заполните опросник");
        if (commands.Any(c => c.UserId != user.Id))
            return Result.Fail("В списке есть задачи другого пользователя");
        if (commands.Select(c => c.Id).Distinct().Count() != commands.Count)
            return Result.Fail("В списке повторяются идентификаторы задач");
        return Result.Ok();
    }

    private static (List<CreateTaskCommand> Valid, List<UnplannedTask> Unplanned) ValidateCommands(
        List<CreateTaskCommand> commands, DateTime now)
    {
        var valid = new List<CreateTaskCommand>();
        var unplanned = new List<UnplannedTask>();
        foreach (var command in commands)
        {
            var error = GetValidationError(command, now);
            if (error is null)
                valid.Add(command);
            else
                unplanned.Add(new UnplannedTask(command.Id, command.Name, error));
        }
        return (valid, unplanned);
    }

    private static (DateTime Start, DateTime End) GetDayBounds(SleepProfile sleep, DateOnly date)
    {
        var schedule = date.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday
            ? sleep.FreeDays : sleep.WorkDays;
        var start = date.ToDateTime(schedule.WakeTime, DateTimeKind.Utc);
        var end = date.ToDateTime(schedule.SleepStart, DateTimeKind.Utc);
        return (start, end <= start ? end.AddDays(1) : end);
    }

    private Task<List<DomainTask>> LoadPlannedTasks(Guid userId, DateTime start, DateTime end)
    {
        var searchStart = start - Gap;
        var searchEnd = end + Gap;
        return _dbContext.Set<DomainTask>().AsNoTracking()
            .Where(t => t.OwnerId == userId && t.From < searchEnd && t.To > searchStart)
            .ToListAsync();
    }

    private List<DomainTask> PlaceTasks(List<CreateTaskCommand> commands,
        PlanningDay day, List<UnplannedTask> unplannedTasks)
    {
        var tasks = new List<DomainTask>();
        var ordered = commands.OrderByDescending(c => GetRank(c, day.Now)).ThenBy(c => c.Deadline);
        foreach (var command in ordered)
        {
            var points = day.Curves[GetCurveKind(command.Type)].Points;
            var slots = GetFreeSlots(day.Start, day.End, day.PlannedTasks);
            var (taskStart, fit) = FindBestStart(command, points, slots, day.Now);
            if (taskStart is null)
            {
                unplannedTasks.Add(new UnplannedTask(command.Id, command.Name,
                    "Нет свободного места до конца дня или дедлайна"));
                continue;
            }

            var task = new DomainTask(command.Id, command.UserId, command.Name, command.Type, command.Duration,
                taskStart.Value, command.Priority, command.Deadline, command.Description);
            tasks.Add(task);
            day.PlannedTasks.Add(task);
            _logger.LogDebug("Подобран слот для задачи {TaskId}: {Start}, Fit: {Fit}", task.Id, taskStart.Value, fit);
        }
        return tasks;
    }

    private async Task SavePlannedTasks(List<DomainTask> tasks)
    {
        if (tasks.Count == 0)
            return;
        await _dbContext.Set<DomainTask>().AddRangeAsync(tasks);
        await _dbContext.SaveChangesAsync();
    }

    private static string? GetValidationError(CreateTaskCommand command, DateTime now)
    {
        if (command.Duration <= TimeSpan.Zero)
            return "Длительность должна быть больше нуля";

        if ((int)command.Priority is < 1 or > 3)
            return "Некорректный приоритет";

        if (!Enum.IsDefined(command.Type))
            return "Некорректная категория";

        if (command.Deadline is { } deadline && deadline <= now)
            return "Срок задачи уже истёк";

        return null;
    }

    private static double GetRank(CreateTaskCommand command, DateTime now)
    {
        var urgency = command.Deadline is { } deadline
            ? Math.Exp(-Math.Max(0, (deadline - now).TotalHours) / 24)
            : 0;

        return 0.6 * ((int)command.Priority / 3.0) + 0.4 * urgency;
    }

    private static EnergyCurveKind GetCurveKind(TaskCategory category)
        => category switch
    {
        TaskCategory.Mental   => EnergyCurveKind.Mental,
        TaskCategory.Physical => EnergyCurveKind.Physical,
        TaskCategory.Social   => EnergyCurveKind.Social,
        TaskCategory.Routine  => EnergyCurveKind.Routine,
        _ => throw new ArgumentOutOfRangeException(nameof(category))
    };

    private List<(DateTime start, DateTime end)> GetFreeSlots(DateTime rangeStart, DateTime rangeEnd, List<DomainTask> plannedTasks)
    {
        var free = new List<(DateTime start, DateTime end)>();
        var cursor = rangeStart;

        foreach (var task in plannedTasks.OrderBy(t => t.From))
        {
            var blockedStart = task.From - Gap;
            var blockedEnd = task.To + Gap;

            if (blockedEnd <= cursor)
                continue;

            if (blockedStart >= rangeEnd)
                break;

            if (blockedStart > cursor)
                free.Add((cursor, blockedStart));

            cursor = blockedEnd > rangeEnd
                ? rangeEnd
                : blockedEnd;

            if (cursor >= rangeEnd)
                break;
        }

        if (cursor < rangeEnd)
            free.Add((cursor, rangeEnd));

        return free;
    }

    private static (DateTime? Start, double Fit) FindBestStart(CreateTaskCommand command,
        IReadOnlyList<EnergyPoint> points, List<(DateTime start, DateTime end)> freeSlots, DateTime now)
    {
        DateTime? bestStart = null;
        var bestFit = double.NegativeInfinity;
        foreach (var start in GetCandidateStarts(command, freeSlots, now))
        {
            var fit = GetAverageEnergy(points, start, start + command.Duration);
            if (double.IsFinite(fit) && fit > bestFit)
            {
                bestFit = fit;
                bestStart = start;
            }
        }
        return (bestStart, bestFit);
    }

    private static IEnumerable<DateTime> GetCandidateStarts(CreateTaskCommand command,
        List<(DateTime start, DateTime end)> freeSlots, DateTime now)
    {
        foreach (var slot in freeSlots)
        {
            var start = slot.start > now ? slot.start : now;
            var latestEnd = slot.end;
            if (command.Deadline is { } deadline && deadline < latestEnd)
                latestEnd = deadline;

            var remainder = start.Ticks % Step.Ticks;
            if (remainder != 0)
                start = start.AddTicks(Step.Ticks - remainder);
            if (start >= latestEnd || command.Duration > latestEnd - start)
                continue;

            var latestStart = latestEnd - command.Duration;
            for (; start <= latestStart; start += Step)
                yield return start;
        }
    }

    private static double GetAverageEnergy(IReadOnlyList<EnergyPoint> points, DateTime start, DateTime end)
    {
        if (points.Count < 2 || start < points[0].Time.UtcDateTime || end > points[^1].Time.UtcDateTime)
            return double.NegativeInfinity;

        var area = 0.0;
        for (var i = 1; i < points.Count; i++)
        {
            var left = points[i - 1];
            var right = points[i];
            if (right.Time.UtcDateTime <= start)
                continue;
            if (left.Time.UtcDateTime >= end)
                break;
            area += GetSegmentArea(left, right, start, end);
        }
        return area / (end - start).TotalMinutes;
    }

    private static double GetSegmentArea(EnergyPoint left, EnergyPoint right, DateTime start, DateTime end)
    {
        var segmentStart = left.Time.UtcDateTime;
        var segmentEnd = right.Time.UtcDateTime;
        var from = start > segmentStart ? start : segmentStart;
        var to = end < segmentEnd ? end : segmentEnd;
        var segmentMinutes = (segmentEnd - segmentStart).TotalMinutes;
        var energyFrom = left.Energy + (right.Energy - left.Energy) *
            (from - segmentStart).TotalMinutes / segmentMinutes;
        var energyTo = left.Energy + (right.Energy - left.Energy) *
            (to - segmentStart).TotalMinutes / segmentMinutes;
        return (energyFrom + energyTo) / 2 * (to - from).TotalMinutes;
    }
}
