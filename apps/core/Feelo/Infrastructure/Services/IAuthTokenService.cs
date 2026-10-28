namespace Infrastructure.Services;

internal interface IAuthTokenService
{
    string GenerateAuthToken(string id);
}