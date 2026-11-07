namespace App.Interfaces;

public interface IAuthTokenService
{
    string GenerateAuthToken(string id);
}