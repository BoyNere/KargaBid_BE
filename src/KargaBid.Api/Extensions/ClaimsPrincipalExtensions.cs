using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace KargaBid.Api.Extensions;

public static class ClaimsPrincipalExtensions
{
    /// <summary>Reads the user id from the JWT "sub" claim.</summary>
    public static Guid GetUserId(this ClaimsPrincipal user)
    {
        var sub = user.FindFirstValue(JwtRegisteredClaimNames.Sub);
        return Guid.TryParse(sub, out var id)
            ? id
            : throw new UnauthorizedAccessException("Token does not contain a valid user id.");
    }
}
