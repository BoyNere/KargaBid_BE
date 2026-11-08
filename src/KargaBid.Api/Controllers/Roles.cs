using KargaBid.Api.Entities;

namespace KargaBid.Api.Controllers;

/// <summary>Compile-time role names for [Authorize(Roles = ...)].</summary>
public static class Roles
{
    public const string Shipper = nameof(UserRole.Shipper);
    public const string Driver = nameof(UserRole.Driver);
}
