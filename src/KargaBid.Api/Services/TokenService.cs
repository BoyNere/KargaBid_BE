using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using KargaBid.Api.Entities;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace KargaBid.Api.Services;

public interface ITokenService
{
    (string Token, DateTime ExpiresAtUtc) CreateToken(User user);
}

public class TokenService : ITokenService
{
    public const string RoleClaim = "role";

    private readonly JwtOptions _options;

    public TokenService(IOptions<JwtOptions> options) => _options = options.Value;

    public (string Token, DateTime ExpiresAtUtc) CreateToken(User user)
    {
        var expires = DateTime.UtcNow.AddMinutes(_options.ExpiryMinutes);

        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new Claim(JwtRegisteredClaimNames.Email, user.Email),
            new Claim(JwtRegisteredClaimNames.Name, user.FullName),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new Claim(RoleClaim, user.Role.ToString())
        };

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_options.SigningKey));
        var token = new JwtSecurityToken(
            issuer: _options.Issuer,
            audience: _options.Audience,
            claims: claims,
            notBefore: DateTime.UtcNow,
            expires: expires,
            signingCredentials: new SigningCredentials(key, SecurityAlgorithms.HmacSha256));

        return (new JwtSecurityTokenHandler().WriteToken(token), expires);
    }
}
