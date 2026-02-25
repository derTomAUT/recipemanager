using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;

namespace RecipeManager.Api.Infrastructure.Auth;

public class JwtTokenGenerator
{
    private readonly string _jwtSecret;
    private readonly string _jwtIssuer;
    private readonly string _jwtAudience;
    private readonly int _expiryMinutes;

    public JwtTokenGenerator(IConfiguration config)
    {
        _jwtSecret = config["Jwt:Secret"]
            ?? throw new InvalidOperationException("Jwt:Secret not configured");
        _jwtIssuer = config["Jwt:Issuer"]
            ?? throw new InvalidOperationException("Jwt:Issuer not configured");
        _jwtAudience = config["Jwt:Audience"]
            ?? throw new InvalidOperationException("Jwt:Audience not configured");

        var expiryStr = config["Jwt:ExpiryMinutes"]
            ?? throw new InvalidOperationException("Jwt:ExpiryMinutes not configured");
        _expiryMinutes = int.Parse(expiryStr);

        if (_jwtSecret.Length < 32)
            throw new InvalidOperationException("Jwt:Secret must be at least 32 characters");
    }

    public string Generate(Guid userId, string email, Guid? householdId, string? role)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, userId.ToString()),
            new(ClaimTypes.Email, email)
        };

        if (householdId.HasValue)
        {
            claims.Add(new("household_id", householdId.Value.ToString()));
            if (role != null)
                claims.Add(new(ClaimTypes.Role, role));
        }

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwtSecret));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var expiry = DateTime.UtcNow.AddMinutes(_expiryMinutes);

        var token = new JwtSecurityToken(
            issuer: _jwtIssuer,
            audience: _jwtAudience,
            claims: claims,
            expires: expiry,
            signingCredentials: creds
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
