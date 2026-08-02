using System.Net.Http.Json;
using Api.Data;
using Api.Data.Seeding;
using Api.DTOs.Auth;
using Api.Models;
using Api.Models.Enums;
using Api.Services.Crypto;
using Api.Services.Mfa;
using OtpNet;

namespace IntegrationTests;

[Collection(IntegrationTestCollection.Name)]
public sealed class MfaLoginIntegrationTests(IntegrationTestFactory factory)
{
    [Fact]
    public async Task TotpAndRecoveryLogin_ConsumeTicketAndFactorExactlyOnce()
    {
        await factory.ResetAsync();
        const string email = "mfa-login-flow@example.com";
        const string password = "V4lid!River-Stone-Cobalt-47";
        var cancellationToken = TestContext.Current.CancellationToken;
        var userId = await SeedPasswordUserAsync(email, password);
        var enrollment = await factory.InScopeAsync(services =>
            services.GetRequiredService<ITotpService>().EnrollAsync(userId, cancellationToken));
        var totp = new Totp(Base32Encoding.ToBytes(enrollment.Secret));
        var confirmationCode = totp.ComputeTotp(factory.Clock.GetUtcNow().UtcDateTime);
        var recovery = await factory.InScopeAsync(services =>
            services.GetRequiredService<ITotpService>().ConfirmAsync(userId, confirmationCode, cancellationToken));
        factory.Clock.Advance(TimeSpan.FromSeconds(30));
        var client = factory.CreateClient();

        var totpChallenge = await BeginLoginAsync(client, email, password, cancellationToken);
        var totpCompletion = await client.PostAsJsonAsync(
            "/api/v1/auth/login/mfa",
            new MfaLoginRequest
            {
                MfaTicket = totpChallenge.MfaTicket,
                Code = totp.ComputeTotp(factory.Clock.GetUtcNow().UtcDateTime),
            },
            cancellationToken);
        Assert.Equal(HttpStatusCode.OK, totpCompletion.StatusCode);
        var totpLogin = await totpCompletion.Content.ReadFromJsonAsync<LoginResponse>(cancellationToken);
        Assert.NotNull(totpLogin?.AccessToken);

        var ticketReplay = await client.PostAsJsonAsync(
            "/api/v1/auth/login/mfa",
            new MfaLoginRequest
            {
                MfaTicket = totpChallenge.MfaTicket,
                Code = totp.ComputeTotp(factory.Clock.GetUtcNow().UtcDateTime),
            },
            cancellationToken);
        Assert.Equal(HttpStatusCode.BadRequest, ticketReplay.StatusCode);

        var recoveryChallenge = await BeginLoginAsync(client, email, password, cancellationToken);
        var recoveryCompletion = await client.PostAsJsonAsync(
            "/api/v1/auth/login/mfa",
            new MfaLoginRequest { MfaTicket = recoveryChallenge.MfaTicket, Code = recovery.Codes[0] },
            cancellationToken);
        Assert.Equal(HttpStatusCode.OK, recoveryCompletion.StatusCode);

        var replayChallenge = await BeginLoginAsync(client, email, password, cancellationToken);
        var recoveryReplay = await client.PostAsJsonAsync(
            "/api/v1/auth/login/mfa",
            new MfaLoginRequest { MfaTicket = replayChallenge.MfaTicket, Code = recovery.Codes[0] },
            cancellationToken);
        Assert.Equal(HttpStatusCode.BadRequest, recoveryReplay.StatusCode);
    }

    private static async Task<MfaChallengeResponse> BeginLoginAsync(
        HttpClient client,
        string email,
        string password,
        CancellationToken cancellationToken)
    {
        var response = await client.PostAsJsonAsync(
            "/api/v1/auth/login",
            new LoginRequest { Email = email, Password = password },
            cancellationToken);
        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<MfaChallengeResponse>(cancellationToken))!;
    }

    private async Task<Guid> SeedPasswordUserAsync(string email, string password)
    {
        var userId = Guid.CreateVersion7();
        await factory.InScopeAsync(async services =>
        {
            var database = services.GetRequiredService<AppDbContext>();
            database.Users.Add(new User
            {
                Id = userId,
                Email = email,
                EmailVerified = true,
                PasswordHash = services.GetRequiredService<IPasswordHasher>().Hash(password),
            });
            database.UserRoles.Add(new UserRole { UserId = userId, RoleId = RoleSeed.UserRoleId });
            await database.SaveChangesAsync(TestContext.Current.CancellationToken);
        });
        return userId;
    }
}
