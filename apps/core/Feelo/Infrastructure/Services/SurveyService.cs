using App.Commands;
using App.Interfaces;
using Domain.Entities.User;
using FluentResults;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Services;

public class SurveyService : ISurveyService
{
    private readonly ILogger<SurveyService> _logger;
    private readonly DBContext _dbContext;
    
    public SurveyService(ILogger<SurveyService> logger, DBContext dbContext)
    {
        _logger = logger;
        _dbContext = dbContext;
    }
    
    public async Task<Result> SubmitSurvey(SubmitSurveyCommand command)
    {
        var user = await _dbContext.Users.FindAsync(command.UserId);
        if (user == null)
            return Result.Fail("Пользователь не найден");
        var sleepProfile = new SleepProfile(command.WorkDaySleepSchedule, command.FreeDaySleepSchedule,
            command.DesiredSleepDuration, command.AlarmUsage, command.RequiredWakeTime);
        var rhythmPreferences = new RhythmPreferences(command.GeneralPeak, command.MentalPeak, command.PhysicalPeak,
            command.SocialPeak, command.RoutinePeak, command.AfternoonDip);
        user.ApplySurveyAnswers(rhythmPreferences, sleepProfile);
        await _dbContext.SaveChangesAsync();
        _logger.LogInformation("Обновлены результаты опроса пользователя {UserId}", user.Id);
        return Result.Ok();
    }
}