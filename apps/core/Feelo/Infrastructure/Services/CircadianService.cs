using App.Commands;
using App.Interfaces;
using Domain.Entities.Circadian;
using FluentResults;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Services;

public class CircadianService : ICircadianService
{
    private readonly ILogger<CircadianService> _logger;
    private readonly DBContext _dbContext;

    private static readonly TimeZoneInfo CalculationTimeZone = TimeZoneInfo.Utc;

    public CircadianService(ILogger<CircadianService> logger, DBContext dbContext)
    {
        _logger = logger;
        _dbContext = dbContext;
    }

    public async Task<Result<List<CircadianCurve>>> GetCircadianCurves(
        GetCircadianCommand command)
    {
        var user = await _dbContext.Users
            .AsNoTracking()
            .SingleOrDefaultAsync(u => u.Id == command.UserId);

        if (user is null)
            return Result.Fail("Пользователь не найден");

        if (user.SleepProfile is null || user.RhythmPreferences is null)
            return Result.Fail("Сначала заполните режим сна и опросник");

        var today = TimeZoneInfo.ConvertTime(
            DateTimeOffset.UtcNow,
            CalculationTimeZone
        );

        var date = command.Date
                   ?? DateOnly.FromDateTime(today.DateTime);

        try
        {
            var curves = CircadianCalculator.Calculate(
                user.SleepProfile,
                user.RhythmPreferences,
                date,
                CalculationTimeZone
            );

            _logger.LogInformation(
                "Рассчитан ритм пользователя {UserId} на {Date}",
                user.Id,
                date
            );

            return Result.Ok(curves);
        }
        catch (ArgumentException error)
        {
            return Result.Fail(error.Message);
        }
    }
}