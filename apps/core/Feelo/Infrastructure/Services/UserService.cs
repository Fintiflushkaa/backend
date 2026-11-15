using App.Interfaces;
using Domain.Entities.User;
using FluentResults;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Services;

public class UserService : IUserService
{
    private readonly ILogger<UserService> _logger;
    private readonly DBContext _dbContext;
    
    public UserService(ILogger<UserService> logger, DBContext dbContext)
    {
        _logger = logger;
        _dbContext = dbContext;
    }
    
    public async Task<Result<User>> GetUserById(Guid userId)
    {
        var user = await _dbContext.Users.SingleOrDefaultAsync(u => u.Id == userId);
        if (user is null)
            return Result.Fail("Пользователь не найден");
        _logger.LogInformation("Найден пользователь с id {Id}", userId);
        return user;
    }
}