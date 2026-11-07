using App.Commands;
using App.Interfaces;
using Domain.Entities.User;
using FluentResults;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace Infrastructure.Services;

public class AuthService : IAuthService
{
    private readonly ILogger<AuthService> _logger;
    private readonly DBContext _dbContext;
    private readonly PasswordHasher<string> _passwordHasher;
    private readonly IAuthTokenService _authTokenService;
    
    public AuthService(ILogger<AuthService> logger, DBContext dbContext,
        PasswordHasher<string> passwordHasher, IAuthTokenService authTokenService)
    {
        _logger = logger;
        _dbContext = dbContext;
        _passwordHasher = passwordHasher;
        _authTokenService = authTokenService;
    }
    
    public async Task<Result> Register(RegisterCommand command)
    {
        if (await _dbContext.Users.AnyAsync(u => u.Email == command.Email))
            return Result.Fail("Пользователь с таким email уже существует");
        var hashPassword = _passwordHasher.HashPassword(command.Email, command.Password);
        var newUser = new User(command.Name, command.Email, hashPassword);
        _dbContext.Users.Add(newUser);
        try
        {
            await _dbContext.SaveChangesAsync();
        }
        catch (DbUpdateException error) when (error.InnerException is PostgresException
               { SqlState: PostgresErrorCodes.UniqueViolation, ConstraintName: "IX_Users_Email" })
        {
            _dbContext.Entry(newUser).State = EntityState.Detached;
            return Result.Fail("Пользователь с таким email уже существует");
        }
        _logger.LogInformation("Создан пользователь с id {Id}", newUser.Id);
        return Result.Ok();
    }

    public async Task<Result<string>> Login(LoginCommand command)
    {
        var user = await _dbContext.Users.FirstOrDefaultAsync(x => x.Email == command.Email);
        if (user is null)
            return Result.Fail("Неверный email или пароль");
        var result = _passwordHasher.VerifyHashedPassword(command.Email, user.PasswordHash, command.Password);
        if (result is PasswordVerificationResult.Failed)
            return Result.Fail("Неверный email или пароль");
        return Result.Ok(_authTokenService.GenerateAuthToken(user.Id.ToString()));
    }
}
