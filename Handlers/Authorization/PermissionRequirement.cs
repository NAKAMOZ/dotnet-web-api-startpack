using Microsoft.AspNetCore.Authorization;

namespace Api.Handlers.Authorization;

/// <summary>A single permission that the caller's roles must grant.</summary>
/// <param name="Permission">A constant from <see cref="Permissions"/>.</param>
public sealed record PermissionRequirement(string Permission) : IAuthorizationRequirement;
