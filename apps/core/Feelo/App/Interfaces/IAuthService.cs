using App.Commands;
using FluentResults;

namespace App.Interfaces;

public interface IAuthService
{
    public Task<Result> Register(RegisterCommand command);
    
    public Task<Result<string>> Login(LoginCommand command);
}