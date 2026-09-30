using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using HimLedger.Domain.Entities;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;

namespace HimLedger.Infrastructure.Services;

public class JwtTokenService(IConfiguration config)
{
    public string GenerateToken(User user)
    {
        var secretKey = config["JwtSettings:Secret"]
            ?? throw new InvalidOperationException("JwtSettings:Secret must be configured.");
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, user.UserId.ToString()),
            new Claim(ClaimTypes.Email, user.Email),
            new Claim(ClaimTypes.Name, $"{user.FirstName} {user.LastName}"),
            new Claim(ClaimTypes.Role, user.Role?.Name ?? "Employee"),
            new Claim("DepartmentId", user.DepartmentId?.ToString() ?? "0")
        };

        var token = new JwtSecurityToken(
            issuer: config["JwtSettings:Issuer"],
            audience: config["JwtSettings:Audience"],
            claims: claims,
            expires: DateTime.UtcNow.AddHours(8),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}