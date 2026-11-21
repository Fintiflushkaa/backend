using App.Commands;
using App.Interfaces;
using Domain.Entities.User;
using FluentAssertions;
using Infrastructure;
using Infrastructure.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Tests.ObjectMothers;

namespace Tests.Services;

[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
public class AuthServiceTests
{
    private readonly DBContext _dbContext;
    private readonly IPasswordHasher<string> _passwordHasher;
    private readonly IAuthTokenService _authTokenService;
    private readonly AuthService _authService;
    private User _user = null!;
    private RegisterCommand _registerCommand = null!;
    private LoginCommand _loginCommand = null!;
    private string _token = null!;
    private string _passwordHash = null!;

    public AuthServiceTests()
    {
        var options = new DbContextOptionsBuilder<DBContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _dbContext = new DBContext(options);
        _passwordHasher = Substitute.For<IPasswordHasher<string>>();
        _authTokenService = Substitute.For<IAuthTokenService>();
        _authService = new AuthService(NullLogger<AuthService>.Instance, _dbContext,
            _passwordHasher, _authTokenService);
    }

    [SetUp]
    public void Setup()
    {
        var email = StringMother.Create();
        var password = StringMother.Create();
        _registerCommand = new RegisterCommand(StringMother.Create(), email, password);
        _loginCommand = new LoginCommand(email, password);
        _token = StringMother.Create(25);
        _passwordHash = "hash-" + StringMother.Create();
        _user = new User(_registerCommand.Name, email, _passwordHash);
        _passwordHasher.HashPassword(email, password).Returns(_passwordHash);
        _passwordHasher.VerifyHashedPassword(email, _passwordHash, password)
            .Returns(PasswordVerificationResult.Success);
        _authTokenService.GenerateAuthToken(_user.Id.ToString()).Returns(_token);
    }

    [Test]
    public async Task Should_not_register_when_email_already_exists()
    {
        // arrange
        _dbContext.Users.Add(_user);
        await _dbContext.SaveChangesAsync();

        // act
        var result = await _authService.Register(_registerCommand);

        // assert
        result.IsFailed.Should().BeTrue();
        result.Errors.Should().ContainSingle().Which.Message.Should()
            .Be("Пользователь с таким email уже существует");
        (await _dbContext.Users.CountAsync()).Should().Be(1);
        _passwordHasher.DidNotReceiveWithAnyArgs().HashPassword(null!, null!);
        _authTokenService.DidNotReceiveWithAnyArgs().GenerateAuthToken(null!);
    }

    [Test]
    public async Task Should_register_unique_email_and_persist_hashed_password()
    {
        // arrange

        // act
        var result = await _authService.Register(_registerCommand);

        // assert
        result.IsSuccess.Should().BeTrue();
        result.Errors.Should().BeEmpty();
        _dbContext.ChangeTracker.Clear();
        var saved = await _dbContext.Users.SingleAsync();
        saved.Id.Should().NotBeEmpty();
        saved.Name.Should().Be(_registerCommand.Name);
        saved.Email.Should().Be(_registerCommand.Email);
        saved.PasswordHash.Should().Be(_passwordHash).And.NotBe(_registerCommand.Password);
        _passwordHasher.Received(1).HashPassword(_registerCommand.Email, _registerCommand.Password);
        _authTokenService.DidNotReceiveWithAnyArgs().GenerateAuthToken(null!);
    }

    [Test]
    public async Task Should_not_login_when_user_does_not_exist()
    {
        // arrange

        // act
        var result = await _authService.Login(_loginCommand);

        // assert
        result.IsFailed.Should().BeTrue();
        result.Errors.Should().ContainSingle().Which.Message.Should().Be("Неверный email или пароль");
        _passwordHasher.DidNotReceiveWithAnyArgs().VerifyHashedPassword(null!, null!, null!);
        _authTokenService.DidNotReceiveWithAnyArgs().GenerateAuthToken(null!);
    }

    [Test]
    public async Task Should_not_generate_token_when_password_is_incorrect()
    {
        // arrange
        _dbContext.Users.Add(_user);
        await _dbContext.SaveChangesAsync();
        _passwordHasher.VerifyHashedPassword(_loginCommand.Email, _passwordHash, _loginCommand.Password)
            .Returns(PasswordVerificationResult.Failed);

        // act
        var result = await _authService.Login(_loginCommand);

        // assert
        result.IsFailed.Should().BeTrue();
        result.Errors.Should().ContainSingle().Which.Message.Should().Be("Неверный email или пароль");
        _passwordHasher.Received(1).VerifyHashedPassword(_loginCommand.Email, _passwordHash, _loginCommand.Password);
        _authTokenService.DidNotReceiveWithAnyArgs().GenerateAuthToken(null!);
    }

    [TestCase(PasswordVerificationResult.Success)]
    [TestCase(PasswordVerificationResult.SuccessRehashNeeded)]
    public async Task Should_return_token_when_password_verification_succeeds(PasswordVerificationResult verification)
    {
        // arrange
        _dbContext.Users.Add(_user);
        await _dbContext.SaveChangesAsync();
        _passwordHasher.VerifyHashedPassword(_loginCommand.Email, _passwordHash, _loginCommand.Password)
            .Returns(verification);

        // act
        var result = await _authService.Login(_loginCommand);

        // assert
        result.IsSuccess.Should().BeTrue();
        result.Errors.Should().BeEmpty();
        result.Value.Should().Be(_token);
        _passwordHasher.Received(1).VerifyHashedPassword(_loginCommand.Email, _passwordHash, _loginCommand.Password);
        _authTokenService.Received(1).GenerateAuthToken(_user.Id.ToString());
    }

    [TearDown]
    public async Task TearDown()
    {
        await _dbContext.Database.EnsureDeletedAsync();
        await _dbContext.DisposeAsync();
    }
}
