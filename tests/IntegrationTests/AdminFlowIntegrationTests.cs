using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Api.Data;
using Api.Data.Seeding;
using Api.DTOs.Admin;
using Api.DTOs.Common;
using Api.Handlers.Authorization;
using Api.Models;
using Api.Models.Enums;
using Microsoft.EntityFrameworkCore;

namespace IntegrationTests;

[Collection(IntegrationTestCollection.Name)]
public sealed class AdminFlowIntegrationTests(IntegrationTestFactory factory)
{
    [Fact]
    public async Task UserPagingFilteringSortingRoleLifecycleAndAuditQuery_RunEndToEnd()
    {
        await factory.ResetAsync();
        var cancellationToken = TestContext.Current.CancellationToken;
        var adminId = await SeedUserAsync("admin-matrix@example.com", true);
        var alphaId = await SeedUserAsync("alpha-matrix@example.com", true, "Alpha target");
        var betaId = await SeedUserAsync("beta-matrix@example.com", false, "Beta target");
        await factory.InScopeAsync(async services =>
        {
            var database = services.GetRequiredService<AppDbContext>();
            var alpha = await database.Users.SingleAsync(user => user.Id == alphaId, cancellationToken);
            alpha.FailedLoginCount = 5;
            alpha.LockoutEndsAt = factory.Clock.GetUtcNow().AddMinutes(15);
            await database.SaveChangesAsync(cancellationToken);
        });

        var token = await factory.IssueAccessTokenAsync(
            adminId,
            Guid.CreateVersion7(),
            cancellationToken,
            [Roles.Admin]);
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var grant = await client.PostAsJsonAsync(
            $"/api/v1/admin/users/{alphaId}/roles",
            new AssignRoleRequest { RoleId = RoleSeed.AdminRoleId },
            cancellationToken);
        Assert.Equal(HttpStatusCode.NoContent, grant.StatusCode);

        var firstPage = await client.GetFromJsonAsync<PagedResponse<AdminUserResponse>>(
            "/api/v1/admin/users?sort=email:asc&page=1&pageSize=2",
            cancellationToken);
        Assert.Equal(3, firstPage!.TotalCount);
        Assert.Equal(2, firstPage.TotalPages);
        Assert.Equal(
            firstPage.Items.Select(user => user.Email).Order(StringComparer.Ordinal),
            firstPage.Items.Select(user => user.Email));

        var filtered = await client.GetFromJsonAsync<PagedResponse<AdminUserResponse>>(
            "/api/v1/admin/users?search=Alpha&role=Admin&emailVerified=true&locked=true",
            cancellationToken);
        Assert.Equal(alphaId, Assert.Single(filtered!.Items).Id);

        using var update = new HttpRequestMessage(HttpMethod.Patch, $"/api/v1/admin/users/{alphaId}")
        {
            Content = JsonContent.Create(new AdminUpdateUserRequest
            {
                DisplayName = "Updated Alpha",
                Unlock = true,
            }),
        };
        update.Headers.Add("X-Correlation-Id", "admin-matrix-update");
        var updated = await client.SendAsync(update, cancellationToken);
        Assert.Equal(HttpStatusCode.OK, updated.StatusCode);
        var detail = await updated.Content.ReadFromJsonAsync<AdminUserDetailResponse>(cancellationToken);
        Assert.Equal("Updated Alpha", detail!.DisplayName);
        Assert.Null(detail.LockoutEndsAt);
        Assert.Equal(0, detail.FailedLoginCount);

        var audit = await client.GetFromJsonAsync<JsonElement>(
            $"/api/v1/admin/audit-logs?userId={alphaId}&eventType=AdminUserUpdated&correlationId=admin-matrix-update",
            cancellationToken);
        var updateAudit = Assert.Single(audit.GetProperty("items").EnumerateArray());
        Assert.Equal("AdminUserUpdated", updateAudit.GetProperty("eventType").GetString());

        var revoke = await client.DeleteAsync(
            $"/api/v1/admin/users/{alphaId}/roles/{RoleSeed.AdminRoleId}",
            cancellationToken);
        Assert.Equal(HttpStatusCode.NoContent, revoke.StatusCode);

        var delete = await client.DeleteAsync($"/api/v1/admin/users/{betaId}", cancellationToken);
        Assert.Equal(HttpStatusCode.NoContent, delete.StatusCode);
        await factory.InScopeAsync(async services =>
        {
            var database = services.GetRequiredService<AppDbContext>();
            Assert.False(await database.Users.AnyAsync(user => user.Id == betaId, cancellationToken));
            Assert.True(await database.AuditLogEntries.AnyAsync(
                entry => entry.EventType == AuditEventType.RoleRevoked && entry.UserId == alphaId,
                cancellationToken));
            Assert.True(await database.AuditLogEntries.AnyAsync(
                entry => entry.EventType == AuditEventType.AdminUserDeleted
                         && entry.UserId == null
                         && entry.Metadata != null,
                cancellationToken));
        });
    }

    private async Task<Guid> SeedUserAsync(
        string email,
        bool emailVerified,
        string? displayName = null)
    {
        var userId = Guid.CreateVersion7();
        await factory.InScopeAsync(async services =>
        {
            var database = services.GetRequiredService<AppDbContext>();
            database.Users.Add(new User
            {
                Id = userId,
                Email = email,
                EmailVerified = emailVerified,
                DisplayName = displayName,
            });
            database.UserRoles.Add(new UserRole
            {
                UserId = userId,
                RoleId = email.StartsWith("admin-", StringComparison.Ordinal)
                    ? RoleSeed.AdminRoleId
                    : RoleSeed.UserRoleId,
            });
            await database.SaveChangesAsync(TestContext.Current.CancellationToken);
        });
        factory.Clock.Advance(TimeSpan.FromTicks(1));
        return userId;
    }
}
