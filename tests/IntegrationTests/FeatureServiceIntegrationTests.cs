using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Api.BackgroundServices;
using Api.Data;
using Api.Data.Seeding;
using Api.DTOs.Admin;
using Api.DTOs.ApiKeys;
using Api.DTOs.Auth;
using Api.DTOs.Mfa;
using Api.DTOs.Passkeys;
using Api.DTOs.PasswordReset;
using Api.DTOs.Sessions;
using Api.DTOs.Users;
using Api.Handlers.Authorization;
using Api.Models;
using Api.Models.Enums;
using Api.Services.Crypto;
using Api.Services.Tokens;
using IntegrationTests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using OtpNet;

namespace IntegrationTests;

[Collection(IntegrationTestCollection.Name)]
public sealed class FeatureServiceIntegrationTests(IntegrationTestFactory factory)
{
    [Fact]
    public async Task Registration_DuplicateIsByteIdenticalAndCreatesOneAccount()
    {
        await factory.ResetAsync();
        const string password = "V4lid!River-Stone-Cobalt-47";
        await SeedPasswordUserAsync("existing-register@example.com", password);
        var client = factory.CreateClient();
        var existingRequest = new RegisterRequest
        {
            Email = "existing-register@example.com",
            Password = password,
            DisplayName = "Registered User",
        };
        var newRequest = existingRequest with { Email = "new-register@example.com" };

        var existing = await client.PostAsJsonAsync(
            "/api/v1/auth/register",
            existingRequest,
            TestContext.Current.CancellationToken);
        var absent = await client.PostAsJsonAsync(
            "/api/v1/auth/register",
            newRequest,
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Accepted, existing.StatusCode);
        Assert.Equal(HttpStatusCode.Accepted, absent.StatusCode);
        Assert.Equal(
            await existing.Content.ReadAsStringAsync(TestContext.Current.CancellationToken),
            await absent.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));

        await factory.InScopeAsync(async services =>
        {
            var database = services.GetRequiredService<AppDbContext>();
            var user = await database.Users
                .Include(candidate => candidate.UserRoles)
                .SingleAsync(
                    candidate => candidate.Email == newRequest.Email,
                    TestContext.Current.CancellationToken);

            Assert.NotNull(user.PasswordHash);
            Assert.False(user.EmailVerified);
            Assert.Single(user.UserRoles);
            Assert.Equal(2, await database.Users.CountAsync(TestContext.Current.CancellationToken));
        });
    }

    [Fact]
    public async Task Registration_ConcurrentRequestsConvergeOnOneAccount()
    {
        await factory.ResetAsync();
        var request = new RegisterRequest
        {
            Email = "registration-race@example.com",
            Password = "V4lid!River-Stone-Cobalt-47",
        };
        var firstClient = factory.CreateClient();
        var secondClient = factory.CreateClient();

        var responses = await Task.WhenAll(
            firstClient.PostAsJsonAsync(
                "/api/v1/auth/register",
                request,
                TestContext.Current.CancellationToken),
            secondClient.PostAsJsonAsync(
                "/api/v1/auth/register",
                request,
                TestContext.Current.CancellationToken));

        Assert.All(responses, response => Assert.Equal(HttpStatusCode.Accepted, response.StatusCode));
        Assert.Equal(
            await responses[0].Content.ReadAsStringAsync(TestContext.Current.CancellationToken),
            await responses[1].Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        await factory.InScopeAsync(async services =>
        {
            var database = services.GetRequiredService<AppDbContext>();
            Assert.Equal(
                1,
                await database.Users.CountAsync(
                    user => user.Email == request.Email,
                    TestContext.Current.CancellationToken));
        });
    }

    [Fact]
    public async Task LoginRefreshAndReplay_RunThroughTheHttpContract()
    {
        await factory.ResetAsync();
        const string password = "V4lid!River-Stone-Cobalt-47";
        await SeedPasswordUserAsync("login-flow@example.com", password);
        var client = factory.CreateClient();

        var login = await client.PostAsJsonAsync(
            "/api/v1/auth/login",
            new LoginRequest { Email = "login-flow@example.com", Password = password },
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        var loginBody = await login.Content.ReadFromJsonAsync<LoginResponse>(
            TestContext.Current.CancellationToken);
        Assert.NotNull(loginBody?.AccessToken);
        Assert.NotNull(loginBody.RefreshToken);

        var rotated = await client.PostAsJsonAsync(
            "/api/v1/auth/refresh",
            new RefreshRequest { RefreshToken = loginBody.RefreshToken },
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, rotated.StatusCode);

        var replay = await client.PostAsJsonAsync(
            "/api/v1/auth/refresh",
            new RefreshRequest { RefreshToken = loginBody.RefreshToken },
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Unauthorized, replay.StatusCode);
        var problem = await replay.Content.ReadFromJsonAsync<JsonElement>(
            TestContext.Current.CancellationToken);
        Assert.Equal("invalid_credentials", problem.GetProperty("errorCode").GetString());
    }

    [Fact]
    public async Task PasswordReset_RotatesStampAndRevokesEverySession()
    {
        await factory.ResetAsync();
        var userId = await SeedPasswordUserAsync(
            "reset-flow@example.com",
            "V4lid!River-Stone-Cobalt-47");
        var sessionId = await factory.InScopeAsync(services =>
            services.GetRequiredService<ISessionService>().CreateAsync(
                new NewSessionRequest
                {
                    UserId = userId,
                    AuthenticationMethods = [AuthenticationMethod.Password],
                },
                TestContext.Current.CancellationToken));
        const string resetToken = "reset-token-value";
        await factory.InScopeAsync(async services =>
        {
            var database = services.GetRequiredService<AppDbContext>();
            var generator = services.GetRequiredService<ITokenGenerator>();
            database.VerificationTokens.Add(new VerificationToken
            {
                UserId = userId,
                Type = VerificationTokenType.PasswordReset,
                TokenHash = generator.Hash(resetToken),
                ExpiresAt = factory.Clock.GetUtcNow().AddHours(1),
            });
            await database.SaveChangesAsync(TestContext.Current.CancellationToken);
        });

        var response = await factory.CreateClient().PostAsJsonAsync(
            "/api/v1/password-reset/confirm",
            new PasswordResetConfirmRequest
            {
                Token = resetToken,
                NewPassword = "N3w!River-Stone-Cobalt-84",
            },
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        await factory.InScopeAsync(async services =>
        {
            var database = services.GetRequiredService<AppDbContext>();
            var hasher = services.GetRequiredService<IPasswordHasher>();
            var user = await database.Users.SingleAsync(
                candidate => candidate.Id == userId,
                TestContext.Current.CancellationToken);
            var session = await database.Sessions.SingleAsync(
                candidate => candidate.Id == sessionId,
                TestContext.Current.CancellationToken);

            Assert.True(hasher.Verify("N3w!River-Stone-Cobalt-84", user.PasswordHash!));
            Assert.Equal(SessionRevocationReason.PasswordReset, session.RevocationReason);
        });
    }

    [Fact]
    public async Task TotpEnrollment_ConfirmsAndReturnsOneTimeRecoveryCodes()
    {
        await factory.ResetAsync();
        var userId = await SeedPasswordUserAsync(
            "mfa-flow@example.com",
            "V4lid!River-Stone-Cobalt-47");
        var accessToken = await factory.IssueAccessTokenAsync(
            userId,
            Guid.CreateVersion7(),
            TestContext.Current.CancellationToken);
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        var enrolled = await client.PostAsync(
            "/api/v1/mfa/totp/enroll",
            content: null,
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, enrolled.StatusCode);
        var enrollment = await enrolled.Content.ReadFromJsonAsync<TotpEnrollmentResponse>(
            TestContext.Current.CancellationToken);
        var code = new Totp(Base32Encoding.ToBytes(enrollment!.Secret))
            .ComputeTotp(factory.Clock.GetUtcNow().UtcDateTime);

        var confirmed = await client.PostAsJsonAsync(
            "/api/v1/mfa/totp/confirm",
            new ConfirmTotpRequest { Code = code },
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, confirmed.StatusCode);
        var recovery = await confirmed.Content.ReadFromJsonAsync<RecoveryCodesResponse>(
            TestContext.Current.CancellationToken);
        Assert.Equal(10, recovery!.Codes.Count);
        Assert.Equal(10, recovery.Codes.Distinct(StringComparer.Ordinal).Count());
    }

    [Fact]
    public async Task Sessions_ListFlagsCurrentAndBulkRevocationPreservesIt()
    {
        await factory.ResetAsync();
        var userId = await SeedPasswordUserAsync(
            "sessions-flow@example.com",
            "V4lid!River-Stone-Cobalt-47");
        var currentSessionId = await CreateSessionAsync(userId);
        var siblingSessionId = await CreateSessionAsync(userId);
        var client = AuthenticatedClient(
            await factory.IssueAccessTokenAsync(
                userId,
                currentSessionId,
                TestContext.Current.CancellationToken));

        var listed = await client.GetFromJsonAsync<List<SessionResponse>>(
            "/api/v1/sessions",
            TestContext.Current.CancellationToken);
        Assert.Equal(2, listed!.Count);
        Assert.True(Assert.Single(listed, session => session.Id == currentSessionId).IsCurrent);
        Assert.False(Assert.Single(listed, session => session.Id == siblingSessionId).IsCurrent);

        var revoked = await client.DeleteAsync(
            "/api/v1/sessions",
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, revoked.StatusCode);
        var body = await revoked.Content.ReadFromJsonAsync<RevokeSessionsResponse>(
            TestContext.Current.CancellationToken);
        Assert.Equal(1, body!.RevokedCount);

        await factory.InScopeAsync(async services =>
        {
            var database = services.GetRequiredService<AppDbContext>();
            var sessions = await database.Sessions
                .Where(session => session.UserId == userId)
                .ToDictionaryAsync(session => session.Id, TestContext.Current.CancellationToken);
            Assert.Null(sessions[currentSessionId].RevokedAt);
            Assert.Equal(
                SessionRevocationReason.UserRevokedAllSessions,
                sessions[siblingSessionId].RevocationReason);
        });
    }

    [Fact]
    public async Task ChangePassword_PreservesCurrentSessionAndRevokesSiblings()
    {
        await factory.ResetAsync();
        const string oldPassword = "V4lid!River-Stone-Cobalt-47";
        const string newPassword = "N3w!River-Stone-Cobalt-84";
        var userId = await SeedPasswordUserAsync("password-change@example.com", oldPassword);
        var currentSessionId = await CreateSessionAsync(userId);
        var siblingSessionId = await CreateSessionAsync(userId);
        var client = AuthenticatedClient(
            await factory.IssueAccessTokenAsync(
                userId,
                currentSessionId,
                TestContext.Current.CancellationToken));

        var response = await client.PutAsJsonAsync(
            "/api/v1/users/me/password",
            new ChangePasswordRequest
            {
                CurrentPassword = oldPassword,
                NewPassword = newPassword,
            },
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        await factory.InScopeAsync(async services =>
        {
            var database = services.GetRequiredService<AppDbContext>();
            var hasher = services.GetRequiredService<IPasswordHasher>();
            var user = await database.Users.SingleAsync(
                candidate => candidate.Id == userId,
                TestContext.Current.CancellationToken);
            var sessions = await database.Sessions
                .Where(session => session.UserId == userId)
                .ToDictionaryAsync(session => session.Id, TestContext.Current.CancellationToken);

            Assert.True(hasher.Verify(newPassword, user.PasswordHash!));
            Assert.Null(sessions[currentSessionId].RevokedAt);
            Assert.Equal(user.SecurityStamp, sessions[currentSessionId].SecurityStamp);
            Assert.Equal(SessionRevocationReason.PasswordChanged, sessions[siblingSessionId].RevocationReason);
        });
    }

    [Fact]
    public async Task ApiKey_SecretIsShownOnceAndSuccessfulUseUpdatesLastUsed()
    {
        await factory.ResetAsync();
        var userId = await SeedPasswordUserAsync(
            "api-key-flow@example.com",
            "V4lid!River-Stone-Cobalt-47",
            RoleSeed.AdminRoleId);
        var bearer = await factory.IssueAccessTokenAsync(
            userId,
            Guid.CreateVersion7(),
            TestContext.Current.CancellationToken,
            [Roles.Admin]);
        var client = AuthenticatedClient(bearer);
        var created = await client.PostAsJsonAsync(
            "/api/v1/api-keys",
            new CreateApiKeyRequest
            {
                Name = "feature-flow",
                Scopes = [Permissions.UsersReadAny],
            },
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var key = await created.Content.ReadFromJsonAsync<CreateApiKeyResponse>(
            TestContext.Current.CancellationToken);
        Assert.StartsWith("ak_", key!.Key, StringComparison.Ordinal);
        Assert.DoesNotContain('_', key.KeyPrefix);

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("ApiKey", key.Key);
        Assert.Equal(
            HttpStatusCode.OK,
            (await client.GetAsync("/api/v1/admin/users", TestContext.Current.CancellationToken)).StatusCode);

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", bearer);
        var summaries = await client.GetFromJsonAsync<List<ApiKeySummaryResponse>>(
            "/api/v1/api-keys",
            TestContext.Current.CancellationToken);
        var summary = Assert.Single(summaries!);
        Assert.NotNull(summary.LastUsedAt);
        Assert.DoesNotContain(
            key.Key,
            JsonSerializer.Serialize(summaries),
            StringComparison.Ordinal);

        Assert.Equal(
            HttpStatusCode.NoContent,
            (await client.DeleteAsync(
                $"/api/v1/api-keys/{key.Id}",
                TestContext.Current.CancellationToken)).StatusCode);
    }

    [Fact]
    public async Task SocialAndPasskeyAnonymousOptions_UseStablePublicContracts()
    {
        await factory.ResetAsync();
        var client = factory.CreateClient();
        var unsupported = await client.GetAsync(
            "/api/v1/auth/social/unknown/authorize",
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.BadRequest, unsupported.StatusCode);
        var problem = await unsupported.Content.ReadFromJsonAsync<JsonElement>(
            TestContext.Current.CancellationToken);
        Assert.Equal("unsupported_provider", problem.GetProperty("errorCode").GetString());

        var optionsResponse = await client.PostAsJsonAsync(
            "/api/v1/passkeys/authentication/options",
            new PasskeyAuthenticationOptionsRequest(),
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, optionsResponse.StatusCode);
        var options = await optionsResponse.Content.ReadFromJsonAsync<PasskeyAuthenticationOptionsResponse>(
            TestContext.Current.CancellationToken);
        Assert.Empty(options!.Options.GetProperty("allowCredentials").EnumerateArray());

        await factory.InScopeAsync(async services =>
        {
            var database = services.GetRequiredService<AppDbContext>();
            Assert.Equal(
                1,
                await database.VerificationTokens.CountAsync(
                    token => token.Type == VerificationTokenType.PasskeyAuthenticationChallenge
                             && token.UserId == null,
                    TestContext.Current.CancellationToken));
        });
    }

    [Fact]
    public async Task AdminRoleAndSessionServices_ApplyChangesAndEmitAudits()
    {
        await factory.ResetAsync();
        const string password = "V4lid!River-Stone-Cobalt-47";
        var adminId = await SeedPasswordUserAsync(
            "admin-flow@example.com",
            password,
            RoleSeed.AdminRoleId);
        var targetId = await SeedPasswordUserAsync("admin-target@example.com", password);
        _ = await CreateSessionAsync(targetId);
        var client = AuthenticatedClient(
            await factory.IssueAccessTokenAsync(
                adminId,
                Guid.CreateVersion7(),
                TestContext.Current.CancellationToken,
                [Roles.Admin]));

        var granted = await client.PostAsJsonAsync(
            $"/api/v1/admin/users/{targetId}/roles",
            new AssignRoleRequest { RoleId = RoleSeed.AdminRoleId },
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.NoContent, granted.StatusCode);

        var revoked = await client.DeleteAsync(
            $"/api/v1/admin/users/{targetId}/sessions",
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, revoked.StatusCode);
        Assert.Equal(
            1,
            (await revoked.Content.ReadFromJsonAsync<RevokeSessionsResponse>(
                TestContext.Current.CancellationToken))!.RevokedCount);

        await factory.InScopeAsync(async services =>
        {
            var database = services.GetRequiredService<AppDbContext>();
            Assert.True(await database.UserRoles.AnyAsync(
                role => role.UserId == targetId && role.RoleId == RoleSeed.AdminRoleId,
                TestContext.Current.CancellationToken));
            Assert.True(await database.AuditLogEntries.AnyAsync(
                entry => entry.EventType == AuditEventType.RoleGranted,
                TestContext.Current.CancellationToken));
            Assert.True(await database.AuditLogEntries.AnyAsync(
                entry => entry.EventType == AuditEventType.SessionRevoked,
                TestContext.Current.CancellationToken));
        });
    }

    [Fact]
    public async Task CleanupWorker_RemovesExpiredArtifactsAndRetainedAuditRows()
    {
        await factory.ResetAsync();
        var userId = await SeedPasswordUserAsync(
            "cleanup-flow@example.com",
            "V4lid!River-Stone-Cobalt-47");
        var now = factory.Clock.GetUtcNow();
        await factory.InScopeAsync(async services =>
        {
            var database = services.GetRequiredService<AppDbContext>();
            var generator = services.GetRequiredService<ITokenGenerator>();
            var session = new Session
            {
                UserId = userId,
                SecurityStamp = "expired-session-stamp",
                AuthenticationMethods = [AuthenticationMethod.Password],
                AuthenticatedAt = now.AddDays(-100),
                LastActiveAt = now.AddDays(-100),
                AbsoluteExpiresAt = now.AddDays(-99),
            };
            database.Sessions.Add(session);
            database.RefreshTokens.Add(new RefreshToken
            {
                SessionId = session.Id,
                TokenHash = generator.Hash("expired-refresh-token"),
                ExpiresAt = now.AddDays(-99),
            });
            database.VerificationTokens.Add(new VerificationToken
            {
                UserId = userId,
                Type = VerificationTokenType.EmailVerification,
                TokenHash = generator.Hash("expired-verification-token"),
                ExpiresAt = now.AddMinutes(-1),
            });
            database.AuditLogEntries.Add(new AuditLogEntry
            {
                UserId = userId,
                EventType = AuditEventType.LoginSucceeded,
                OccurredAt = now.AddDays(-91),
            });
            await database.SaveChangesAsync(TestContext.Current.CancellationToken);
        });

        await factory.InScopeAsync(async services =>
        {
            var worker = services.GetRequiredService<ExpiredAuthArtifactCleanupService>();
            await worker.CleanupOnceAsync(TestContext.Current.CancellationToken);
        });

        await factory.InScopeAsync(async services =>
        {
            var database = services.GetRequiredService<AppDbContext>();
            Assert.Empty(await database.Sessions.ToListAsync(TestContext.Current.CancellationToken));
            Assert.Empty(await database.RefreshTokens.ToListAsync(TestContext.Current.CancellationToken));
            Assert.Empty(await database.VerificationTokens.ToListAsync(TestContext.Current.CancellationToken));
            Assert.Empty(await database.AuditLogEntries.ToListAsync(TestContext.Current.CancellationToken));
        });
    }

    private async Task<Guid> SeedPasswordUserAsync(
        string email,
        string password,
        Guid? roleId = null)
    {
        var userId = Guid.CreateVersion7();
        await factory.InScopeAsync(async services =>
        {
            var database = services.GetRequiredService<AppDbContext>();
            var hasher = services.GetRequiredService<IPasswordHasher>();
            database.Users.Add(new User
            {
                Id = userId,
                Email = email,
                EmailVerified = true,
                PasswordHash = hasher.Hash(password),
            });
            database.UserRoles.Add(new UserRole
            {
                UserId = userId,
                RoleId = roleId ?? RoleSeed.UserRoleId,
            });
            await database.SaveChangesAsync(TestContext.Current.CancellationToken);
        });
        return userId;
    }

    private async Task<Guid> CreateSessionAsync(Guid userId) =>
        await factory.InScopeAsync(services =>
            services.GetRequiredService<ISessionService>().CreateAsync(
                new NewSessionRequest
                {
                    UserId = userId,
                    AuthenticationMethods = [AuthenticationMethod.Password],
                },
                TestContext.Current.CancellationToken));

    private HttpClient AuthenticatedClient(string accessToken)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        return client;
    }
}
