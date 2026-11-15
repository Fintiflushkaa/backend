using Domain.Entities.User;
using FluentResults;

namespace App.Interfaces;

public interface IUserService
{
    public Task<Result<User>> GetUserById(Guid userId);
}