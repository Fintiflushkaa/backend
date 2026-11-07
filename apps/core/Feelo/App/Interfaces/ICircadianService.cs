using App.Commands;
using Domain.Entities.Circadian;
using FluentResults;

namespace App.Interfaces;

public interface ICircadianService
{
    public Task<Result<List<CircadianCurve>>> GetCircadianCurves(GetCircadianCommand command);
}