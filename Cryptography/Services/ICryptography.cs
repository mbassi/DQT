using System.Security.Claims;

namespace DQT.Security.Cryptography.Services
{
    public interface ICryptography
    {
        string GenerateJwtToken(string jwtKey, string jwtEncriptionKey, Claim[] claims, int expiringMinutesFromNow);
        ClaimsPrincipal DecodeJwtToken(string secret, string token, out string errorCode);
        string EncryptString(string rawData);

    }
}
