using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;

namespace Infrastructure.Services.Impl;

public sealed class AuthTokenService(IConfiguration config) : IAuthTokenService
{
    public string GenerateAuthToken(string id)
    {
        var claims = new List<Claim> {new(ClaimTypes.PrimarySid, id)};

        var token = new JwtSecurityToken(
            issuer: "",
            audience: "FeeloAPI",
            claims: claims,
            expires: DateTime.UtcNow.AddDays(7),
            signingCredentials: new SigningCredentials(
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes(config["jwt:Key"]!)),
                SecurityAlgorithms.HmacSha256));

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}