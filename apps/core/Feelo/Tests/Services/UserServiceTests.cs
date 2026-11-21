using App.Interfaces;
using Domain.Entities.User;
using FluentAssertions;
using Infrastructure;
using Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Tests.ObjectMothers;

namespace Tests.Services;

[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
public class UserServiceTests
{
    private readonly DBContext _dbContext;
    private readonly IUserService _userService;
    private User _user = null!;
    private Guid _missingId;

    public UserServiceTests()
    {
        var options = new DbContextOptionsBuilder<DBContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        _dbContext = new DBContext(options);
        _userService = new UserService(NullLogger<UserService>.Instance, _dbContext);
    }

    [SetUp]
    public void Setup()
    {
        _user = new User(StringMother.Create(), StringMother.Create(), StringMother.Create());
        _missingId = Guid.NewGuid();
    }

    [Test]
    public async Task Should_return_error_when_database_has_no_users()
    {
        // arrange

        // act
        var result = await _userService.GetUserById(_missingId);

        // assert
        result.IsFailed.Should().BeTrue();
        result.Errors.Should().ContainSingle().Which.Message.Should().Be("Пользователь не найден");
    }

    [Test]
    public async Task Should_return_requested_user()
    {
        // arrange
        _dbContext.Users.Add(_user);
        await _dbContext.SaveChangesAsync();

        // act
        var result = await _userService.GetUserById(_user.Id);

        // assert
        result.IsSuccess.Should().BeTrue();
        result.Errors.Should().BeEmpty();
        result.Value.Should().BeEquivalentTo(_user);
    }

    [TearDown]
    public async Task TearDown()
    {
        await _dbContext.Database.EnsureDeletedAsync();
        await _dbContext.DisposeAsync();
    }
}
