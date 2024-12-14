using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;

namespace DQT.Security.Cryptography.Services
{
    public class Cryptography : ICryptography
    {
        public string GenerateJwtToken(string jwtKey, string jwtEncriptionKey, Claim[] claims, int expiringMinutesFromNow)
        {
            var jwtSecurityKey = Encoding.UTF8.GetBytes(jwtKey);
            var jwtCredentials = new SigningCredentials(new SymmetricSecurityKey(jwtSecurityKey), SecurityAlgorithms.HmacSha256);
            var jwtToken = new JwtSecurityToken(
                "NFTLaneIsuer",
                "NFTLaneAudience",
                claims,
                null,
                DateTime.Now.AddMinutes(expiringMinutesFromNow),
                jwtCredentials);
            var jwtResult = new JwtSecurityTokenHandler().WriteToken(jwtToken);
            return jwtResult;
        }
        public ClaimsPrincipal DecodeJwtToken(string secret, string token, out string errorCode)
        {
            var key = Encoding.UTF8.GetBytes(secret);
            var handler = new JwtSecurityTokenHandler();
            handler.InboundClaimTypeMap[JwtRegisteredClaimNames.Email] = JwtRegisteredClaimNames.Email;
            handler.InboundClaimTypeMap[JwtRegisteredClaimNames.NameId] = JwtRegisteredClaimNames.NameId;
            var validations = new TokenValidationParameters
            {
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(key),
                ValidateIssuer = true,
                ValidateAudience = true,
                ValidateLifetime = true,
                ValidIssuer = "NFTLaneIsuer",
                ValidAudience = "NFTLaneAudience",
                ClockSkew = TimeSpan.Zero,
            };
            var claimsPrincipal = new ClaimsPrincipal();
            errorCode = "0";
            try
            {
                claimsPrincipal = handler.ValidateToken(token, validations, out var tokenSecure);
            }
            catch (ArgumentNullException)
            {
                errorCode = "NFL-011";
            }
            catch (SecurityTokenExpiredException)
            {
                errorCode = "NFL-012";
            }
            catch (InvalidOperationException)
            {
                errorCode = "NFL-013";
            }
            return claimsPrincipal;

        }

        public string EncryptString(string rawData)
        {
            // Create a SHA256

            using (SHA256 sha256Hash = SHA256.Create())
            {
                // ComputeHash - returns byte array
                byte[] bytes = sha256Hash.ComputeHash(Encoding.UTF8.GetBytes(rawData));

                // Convert byte array to a string

                return Convert.ToBase64String(bytes);
            }
        }

    }
}



