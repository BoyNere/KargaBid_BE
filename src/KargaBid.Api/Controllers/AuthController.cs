using KargaBid.Api.Data;
using KargaBid.Api.Dtos;
using KargaBid.Api.Entities;
using KargaBid.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace KargaBid.Api.Controllers;

[ApiController]
[Route("api/auth")]
[AllowAnonymous]
public class AuthController : ControllerBase
{
    private readonly KargaBidDbContext _db;
    private readonly ITokenService _tokens;
    private readonly IPasswordHasher<User> _hasher;

    public AuthController(KargaBidDbContext db, ITokenService tokens, IPasswordHasher<User> hasher)
    {
        _db = db;
        _tokens = tokens;
        _hasher = hasher;
    }

    // POST /api/auth/register
    [HttpPost("register")]
    [ProducesResponseType<AuthResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Register(RegisterRequest request, CancellationToken ct)
    {
        var email = request.Email.Trim().ToLowerInvariant();

        if (await _db.Users.AnyAsync(u => u.Email == email, ct))
            return Problem(statusCode: StatusCodes.Status409Conflict, title: "Email is already registered.");

        var user = new User
        {
            FullName = request.FullName.Trim(),
            Email = email,
            Role = request.Role,
            CreatedAt = DateTime.UtcNow
        };
        user.PasswordHash = _hasher.HashPassword(user, request.Password);

        _db.Users.Add(user);
        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            // Lost a race against the unique index on Email.
            return Problem(statusCode: StatusCodes.Status409Conflict, title: "Email is already registered.");
        }

        return StatusCode(StatusCodes.Status201Created, BuildAuthResponse(user));
    }

    // POST /api/auth/login
    [HttpPost("login")]
    [ProducesResponseType<AuthResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Login(LoginRequest request, CancellationToken ct)
    {
        var email = request.Email.Trim().ToLowerInvariant();
        var user = await _db.Users.SingleOrDefaultAsync(u => u.Email == email, ct);

        if (user is null)
            return Problem(statusCode: StatusCodes.Status401Unauthorized, title: "Invalid email or password.");

        var result = _hasher.VerifyHashedPassword(user, user.PasswordHash, request.Password);
        if (result == PasswordVerificationResult.Failed)
            return Problem(statusCode: StatusCodes.Status401Unauthorized, title: "Invalid email or password.");

        if (result == PasswordVerificationResult.SuccessRehashNeeded)
        {
            user.PasswordHash = _hasher.HashPassword(user, request.Password);
            await _db.SaveChangesAsync(ct);
        }

        return Ok(BuildAuthResponse(user));
    }

    private AuthResponse BuildAuthResponse(User user)
    {
        var (token, expires) = _tokens.CreateToken(user);
        return new AuthResponse(user.Id, user.FullName, user.Email, user.Role, token, expires);
    }
}
