using System.Net.Http.Headers;
using IntegrationTests.Infrastructure;

namespace IntegrationTests.Security;

[Collection(IntegrationTestCollection.Name)]
[Trait("Category", "Security")]
public sealed class AuthorizationAttackTests(IntegrationTestFactory factory)
{
    private static readonly Guid Id = new("0198f3a0-0000-7000-8001-000000000999");

    public static TheoryData<string, string> ProtectedEndpoints => new()
    {
        { "POST", "/api/v1/auth/logout" },
        { "POST", "/api/v1/email-verification/send" },
        { "GET", "/api/v1/sessions" },
        { "DELETE", $"/api/v1/sessions/{Id}" },
        { "DELETE", "/api/v1/sessions" },
        { "POST", "/api/v1/mfa/totp" },
        { "POST", "/api/v1/mfa/totp/confirm" },
        { "DELETE", "/api/v1/mfa/totp" },
        { "POST", "/api/v1/mfa/recovery-codes/regenerate" },
        { "POST", "/api/v1/passkeys/registration/options" },
        { "POST", "/api/v1/passkeys/registration/complete" },
        { "GET", "/api/v1/passkeys" },
        { "DELETE", $"/api/v1/passkeys/{Id}" },
        { "POST", "/api/v1/api-keys" },
        { "GET", "/api/v1/api-keys" },
        { "DELETE", $"/api/v1/api-keys/{Id}" },
        { "GET", "/api/v1/users/me" },
        { "PATCH", "/api/v1/users/me" },
        { "DELETE", "/api/v1/users/me" },
        { "PUT", "/api/v1/users/me/password" },
        { "GET", "/api/v1/users/me/accounts" },
        { "DELETE", $"/api/v1/users/me/accounts/{Id}" },
        { "GET", "/api/v1/admin/users" },
        { "GET", $"/api/v1/admin/users/{Id}" },
        { "PATCH", $"/api/v1/admin/users/{Id}" },
        { "DELETE", $"/api/v1/admin/users/{Id}" },
        { "POST", $"/api/v1/admin/users/{Id}/roles" },
        { "DELETE", $"/api/v1/admin/users/{Id}/roles/{Id}" },
        { "DELETE", $"/api/v1/admin/users/{Id}/sessions" },
        { "GET", "/api/v1/admin/audit-logs" },
    };

    public static TheoryData<string, string> AdminEndpoints => new()
    {
        { "GET", "/api/v1/admin/users" },
        { "GET", $"/api/v1/admin/users/{Id}" },
        { "PATCH", $"/api/v1/admin/users/{Id}" },
        { "DELETE", $"/api/v1/admin/users/{Id}" },
        { "POST", $"/api/v1/admin/users/{Id}/roles" },
        { "DELETE", $"/api/v1/admin/users/{Id}/roles/{Id}" },
        { "DELETE", $"/api/v1/admin/users/{Id}/sessions" },
        { "GET", "/api/v1/admin/audit-logs" },
    };

    [Theory]
    [MemberData(nameof(ProtectedEndpoints))]
    public async Task ProtectedEndpoint_AnonymousRequest_Is401(string method, string path)
    {
        var response = await factory.CreateClient().SendAsync(
            new HttpRequestMessage(new HttpMethod(method), path),
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Theory]
    [MemberData(nameof(AdminEndpoints))]
    public async Task AdminEndpoint_UserRole_Is403(string method, string path)
    {
        factory.Clock.Advance(TimeSpan.FromTicks(1));
        var token = await factory.IssueAccessTokenAsync(
            Guid.CreateVersion7(),
            Guid.CreateVersion7(),
            TestContext.Current.CancellationToken);
        var request = new HttpRequestMessage(new HttpMethod(method), path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await factory.CreateClient().SendAsync(
            request,
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }
}
