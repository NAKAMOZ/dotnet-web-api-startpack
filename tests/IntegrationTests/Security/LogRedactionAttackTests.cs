using System.Net.Http.Headers;
using System.Net.Http.Json;
using Api.Data;
using Api.Data.Seeding;
using Api.DTOs.ApiKeys;
using Api.DTOs.Auth;
using Api.DTOs.Mfa;
using Api.Handlers.Authorization;
using Api.Models;
using Api.Services.Crypto;
using Microsoft.EntityFrameworkCore;
using OtpNet;

namespace IntegrationTests.Security;

[Collection(IntegrationTestCollection.Name)]
[Trait("Category", "Security")]
public sealed class LogRedactionAttackTests(IntegrationTestFactory factory)
{
    [Fact]
    public async Task FullCredentialFlow_LeaksNoIssuedOrPresentedSecretIntoLogs()
    {
        await factory.ResetAsync();
        const string password = "LogCanary!River-Stone-Cobalt-47";
        var cancellationToken = TestContext.Current.CancellationToken;
        var userId = Guid.CreateVersion7();

        await factory.InScopeAsync(async services =>
        {
            var database = services.GetRequiredService<AppDbContext>();
            var hasher = services.GetRequiredService<IPasswordHasher>();
            database.Users.Add(new User
            {
                Id = userId,
                Email = "log-redaction@example.com",
                EmailVerified = true,
                PasswordHash = hasher.Hash(password),
            });
            database.UserRoles.Add(new UserRole { UserId = userId, RoleId = RoleSeed.AdminRoleId });
            await database.SaveChangesAsync(cancellationToken);
        });

        factory.LogSink.Clear();
        var client = factory.CreateClient();
        var loginResponse = await client.PostAsJsonAsync(
            "/api/v1/auth/login",
            new LoginRequest { Email = "log-redaction@example.com", Password = password },
            cancellationToken);
        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);
        var login = await loginResponse.Content.ReadFromJsonAsync<LoginResponse>(cancellationToken);
        Assert.NotNull(login?.AccessToken);
        Assert.NotNull(login.RefreshToken);

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", login.AccessToken);
        var enrollResponse = await client.PostAsync("/api/v1/mfa/totp/enroll", null, cancellationToken);
        Assert.Equal(HttpStatusCode.OK, enrollResponse.StatusCode);
        var enrollment = await enrollResponse.Content.ReadFromJsonAsync<TotpEnrollmentResponse>(cancellationToken);
        Assert.NotNull(enrollment?.Secret);
        var totpCode = new Totp(Base32Encoding.ToBytes(enrollment.Secret))
            .ComputeTotp(factory.Clock.GetUtcNow().UtcDateTime);
        var confirmResponse = await client.PostAsJsonAsync(
            "/api/v1/mfa/totp/confirm",
            new ConfirmTotpRequest { Code = totpCode },
            cancellationToken);
        var recovery = await confirmResponse.Content.ReadFromJsonAsync<RecoveryCodesResponse>(cancellationToken);
        Assert.Equal(HttpStatusCode.OK, confirmResponse.StatusCode);
        Assert.NotEmpty(recovery!.Codes);

        var keyResponse = await client.PostAsJsonAsync(
            "/api/v1/api-keys",
            new CreateApiKeyRequest
            {
                Name = "redaction-canary",
                Scopes = [Permissions.UsersReadAny],
            },
            cancellationToken);
        Assert.Equal(HttpStatusCode.Created, keyResponse.StatusCode);
        var apiKey = await keyResponse.Content.ReadFromJsonAsync<CreateApiKeyResponse>(cancellationToken);

        var renderedEvents = string.Join(
            '\n',
            factory.LogSink.Snapshot().Select(logEvent =>
                $"{logEvent.RenderMessage()} {string.Join(' ', logEvent.Properties.Values)}"));
        var forbiddenValues = new[]
        {
            password,
            login.AccessToken,
            login.RefreshToken,
            enrollment.Secret,
            totpCode,
            recovery.Codes[0],
            apiKey!.Key,
        };

        Assert.All(forbiddenValues, secret =>
            Assert.DoesNotContain(secret, renderedEvents, StringComparison.Ordinal));
    }
}
