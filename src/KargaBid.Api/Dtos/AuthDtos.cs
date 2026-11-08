using System.ComponentModel.DataAnnotations;
using KargaBid.Api.Entities;

namespace KargaBid.Api.Dtos;

public record RegisterRequest(
    [Required, StringLength(150, MinimumLength = 2)] string FullName,
    [Required, EmailAddress, StringLength(256)] string Email,
    [Required, StringLength(100, MinimumLength = 8)] string Password,
    [Required, EnumDataType(typeof(UserRole))] UserRole Role);

public record LoginRequest(
    [Required, EmailAddress] string Email,
    [Required] string Password);

public record AuthResponse(
    Guid UserId,
    string FullName,
    string Email,
    UserRole Role,
    string AccessToken,
    DateTime ExpiresAtUtc);
