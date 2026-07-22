using Api.Handlers.Authorization;

namespace UnitTests.Authorization;

public class RolePermissionMapTests
{
    [Fact]
    public void AdminGrantsEveryDeclaredPermission()
    {
        foreach (var permission in Permissions.All)
        {
            Assert.True(
                RolePermissionMap.Grants([Roles.Admin], permission),
                $"Admin should grant '{permission}'.");
        }
    }

    [Fact]
    public void UserGrantsNoCrossUserPermission()
    {
        // Deliberate: self-service routes resolve the subject from the `sub` claim and
        // never take a user id, so an ordinary user needs no permission. Any permission
        // granted to User here would apply across all users.
        foreach (var permission in Permissions.All)
        {
            Assert.False(
                RolePermissionMap.Grants([Roles.User], permission),
                $"User must not grant '{permission}'.");
        }
    }

    [Fact]
    public void UnknownRoleGrantsNothing()
    {
        Assert.False(RolePermissionMap.Grants(["Wizard"], Permissions.AuditRead));
    }

    [Fact]
    public void NoRolesGrantsNothing()
    {
        Assert.False(RolePermissionMap.Grants([], Permissions.UsersReadAny));
    }

    [Fact]
    public void AnyGrantingRoleAmongSeveralIsEnough()
    {
        Assert.True(RolePermissionMap.Grants([Roles.User, Roles.Admin], Permissions.AuditRead));
    }

    [Fact]
    public void CatalogAndMapAgree()
    {
        // The same invariant AddAuthorizationServices enforces at startup, asserted here so
        // it fails in a fast test rather than only when the host boots.
        Assert.Equal(
            Permissions.All.OrderBy(static p => p, StringComparer.Ordinal),
            RolePermissionMap.AllMappedPermissions().OrderBy(static p => p, StringComparer.Ordinal));
    }

    [Fact]
    public void PermissionConstantsAreUnique()
    {
        Assert.Equal(Permissions.All.Count, Permissions.All.Distinct(StringComparer.Ordinal).Count());
    }
}
