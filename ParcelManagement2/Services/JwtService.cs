using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using ParcelManagement2.Models;

namespace ParcelManagement2.Services
{
    public interface IJwtService
    {
        string GenerateToken(Account acc, int hours = 8);
    }

    public class JwtService : IJwtService
    {
        private readonly JwtSettings _jwtSettings;
        private readonly byte[] _key;

        public JwtService(IConfiguration configuration, Microsoft.Extensions.Options.IOptions<JwtSettings> jwtOptions)
        {
            _jwtSettings = jwtOptions.Value;
            // 取得 JWT 金鑰並轉換為 byte array
            _key = Encoding.UTF8.GetBytes(_jwtSettings.JwtKey);
        }

        // 專責產生 Token
        public string GenerateToken(Account acc, int hours = 8)
        {
            var tokenHandler = new JwtSecurityTokenHandler();
            var tokenDesc = new SecurityTokenDescriptor
            {
                Subject = new ClaimsIdentity(new[]
                {
                    new Claim(ClaimTypes.Name, acc.Username),
                    new Claim(ClaimTypes.Role, acc.Role),
                    new Claim("CondoId", acc.Condo_Id ?? "")
                }),
                Expires = DateTime.UtcNow.AddHours(hours),
                Issuer = _jwtSettings.JwtIssuer,
                Audience = _jwtSettings.JwtAudience,
                SigningCredentials = new SigningCredentials(
                     new SymmetricSecurityKey(_key),
                     SecurityAlgorithms.HmacSha256Signature)
            };
            var token = tokenHandler.CreateToken(tokenDesc);
            return tokenHandler.WriteToken(token);
        }

    }
}
