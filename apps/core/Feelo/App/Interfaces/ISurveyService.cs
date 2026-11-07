using App.Commands;
using FluentResults;

namespace App.Interfaces;

public interface ISurveyService
{
    public Task<Result> SubmitSurvey(SubmitSurveyCommand command);
}