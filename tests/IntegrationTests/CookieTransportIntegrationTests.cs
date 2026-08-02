using System.Net.Http.Json;
using Api.Data;
using Api.Data.Seeding;
using Api.DTOs.Auth;
using Api.Models;
using Api.Services.Crypto;

namespace IntegrationTests;

[Collection(IntegrationTestCollection.Name)]
public sealed class CookieTransportIntegrationTests(IntegrationTestFactory factory)
{
    [Fact]
    public async Task LoginRefreshCsrfAndLogout_UseOnlyHardenedCookies()
    {
        await factory.ResetAsync();
        const string email = "cookie-flow@example.com";
        const string password = "V4lid!River-Stone-Cobalt-47";
        var cancellationToken = TestContext.Current.CancellationToken;
        await SeedPasswordUserAsync(email, password);
        var client = factory.CreateClient();

        using var loginRequest = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/login")
        {
            Content = JsonContent.Create(new LoginRequest { Email = email, Password = password }),
        };
        loginRequest.Headers.Add("X-Auth-Transport", "cookie");
        var loginResponse = await client.SendAsync(loginRequest, cancellationToken);
        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);
        var login = await loginResponse.Content.ReadFromJsonAsync<LoginResponse>(cancellationToken);
        Assert.Null(login!.AccessToken);
        Assert.Null(login.RefreshToken);
        Assert.Contains(
            loginResponse.Headers.GetValues("Set-Cookie"),
            value => value.StartsWith("__Host-auth.access=", StringComparison.Ordinal)
                     && value.Contains("httponly", StringComparison.OrdinalIgnoreCase)
                     && value.Contains("samesite=lax", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(
            loginResponse.Headers.GetValues("Set-Cookie"),
            value => value.StartsWith("__Secure-auth.refresh=", StringComparison.Ordinal)
                     && value.Contains("path=/api/v1/auth/refresh", StringComparison.OrdinalIgnoreCase)
                     && value.Contains("samesite=strict", StringComparison.OrdinalIgnoreCase));

        await SetFreshCsrfHeaderAsync(client, cancellationToken);
        var refresh = await client.PostAsJsonAsync(
            "/api/v1/auth/refresh",
            new RefreshRequest(),
            cancellationToken);
        Assert.Equal(HttpStatusCode.OK, refresh.StatusCode);
        var refreshed = await refresh.Content.ReadFromJsonAsync<TokenPairResponse>(cancellationToken);
        Assert.Null(refreshed!.AccessToken);
        Assert.Null(refreshed.RefreshToken);

        await SetFreshCsrfHeaderAsync(client, cancellationToken);
        var logout = await client.PostAsync("/api/v1/auth/logout", null, cancellationToken);
        Assert.Equal(HttpStatusCode.NoContent, logout.StatusCode);

        var afterLogout = await client.GetAsync("/api/v1/sessions", cancellationToken);
        Assert.Equal(HttpStatusCode.Unauthorized, afterLogout.StatusCode);
    }

    private async Task SeedPasswordUserAsync(string email, string password)
    {
        await factory.InScopeAsync(async services =>
        {
            var database = services.GetRequiredService<AppDbContext>();
            var user = new User
            {
                Email = email,
                EmailVerified = true,
                PasswordHash = services.GetRequiredService<IPasswordHasher>().Hash(password),
            };
            database.Users.Add(user);
            database.UserRoles.Add(new UserRole { UserId = user.Id, RoleId = RoleSeed.UserRoleId });
            await database.SaveChangesAsync(TestContext.Current.CancellationToken);
        });
    }

    private static async Task SetFreshCsrfHeaderAsync(HttpClient client, CancellationToken cancellationToken)
    {
        client.DefaultRequestHeaders.Remove("X-CSRF-Token");
        var response = await client.GetFromJsonAsync<CsrfTokenResponse>(
            "/api/v1/auth/csrf",
            cancellationToken);
        client.DefaultRequestHeaders.Add(response!.HeaderName, response.Token);
    }
}
