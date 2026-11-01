namespace API.Dto;

internal sealed record SurveyAnswerDto
{
    public TimeSpan WakeTime { get; set; }
    public TimeSpan SleepTime { get; set; }
    
    public TimeSpan FreeDayWakeTime { get; set; }
    public TimeSpan FreeDaySleepTime { get; set; }
    
    public required List<CircadianWindowDto> PeakPeriods { get; set; }
}