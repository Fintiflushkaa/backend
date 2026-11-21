using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using App.Interfaces;
using FluentAssertions;
using Infrastructure.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using NSubstitute;
using Tests.ObjectMothers;

namespace Tests.Services;

[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
public class AuthTokenServiceTests
{
    private readonly IConfiguration _configuration;
    private readonly IAuthTokenService _authTokenService;
    private string _key = null!;
    private string _userId = null!;

    public AuthTokenServiceTests()
    {
        _configuration = Substitute.For<IConfiguration>();
        _authTokenService = new AuthTokenService(_configuration);
    }

    [SetUp]
    public void Setup()
    {
        _key = StringMother.Create(64);
        _userId = Guid.NewGuid().ToString();
        _configuration["jwt:Key"].Returns(_key);
    }

    [Test]
    public void Should_generate_token_with_user_id_audience_and_seven_day_expiration()
    {
        // arrange
        var before = DateTime.UtcNow;

        // act
        var result = _authTokenService.GenerateAuthToken(_userId);

        // assert
        var token = new JwtSecurityTokenHandler().ReadJwtToken(result);
        token.Claims.Should().ContainSingle(c => c.Type == ClaimTypes.PrimarySid && c.Value == _userId);
        token.Audiences.Should().ContainSingle().Which.Should().Be("FeeloAPI");
        token.ValidTo.Should().BeOnOrAfter(before.AddDays(7).AddSeconds(-1))
            .And.BeOnOrBefore(DateTime.UtcNow.AddDays(7));
    }

    [Test]
    public void Should_sign_token_with_configured_key()
    {
        // arrange
        var validation = new TokenValidationParameters
        {
            ValidateIssuer = false,
            ValidAudience = "FeeloAPI",
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_key)),
            ValidAlgorithms = [SecurityAlgorithms.HmacSha256],
            ClockSkew = TimeSpan.Zero
        };

        // act
        var result = _authTokenService.GenerateAuthToken(_userId);

        // assert
        var principal = new JwtSecurityTokenHandler().ValidateToken(result, validation, out _);
        principal.FindFirst(ClaimTypes.PrimarySid)!.Value.Should().Be(_userId);
    }
}
