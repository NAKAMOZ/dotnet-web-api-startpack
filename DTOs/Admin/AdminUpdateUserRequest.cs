namespace Api.DTOs.Admin;

/// <summary>
/// Body for <c>PATCH /api/v1/admin/users/{userId}</c>. Every property is optional — null
/// means "leave unchanged", which is what makes this a patch rather than a replace.
/// </summary>
/// <remarks>
/// Notably absent: password, roles and email. Passwords are never set by an administrator —
/// that would create a credential the user did not choose and the admin knows. Roles have
/// their own endpoints so each grant is a separately audited event.
/// </remarks>
public sealed record AdminUpdateUserRequest
{
    public string? DisplayName { get; init; }

    /// <summary>
    /// Force-verify or un-verify an address. Support uses it when a verification email
    /// cannot be delivered; it is audited like any other administrative change.
    /// </summary>
    public bool? EmailVerified { get; init; }

    /// <summary>
    /// Set to <see langword="true"/> to clear an active lockout and reset the failure
    /// counter. There is no field to <em>impose</em> a lockout — lockout is a consequence of
    /// failed logins, and a manual one would be a disable-account feature wearing the wrong
    /// name.
    /// </summary>
    public bool? Unlock { get; init; }
}
