using System.Net.Http.Json;
using Api.DTOs.Auth;
using Api.DTOs.EmailVerification;
using Api.DTOs.PasswordReset;

namespace IntegrationTests;

[Collection(IntegrationTestCollection.Name)]
public sealed class EmailFlowIntegrationTests(IntegrationTestFactory factory)
{
    [Fact]
    public async Task Registration_EmailConfirmation_LoginAndSingleUse_RunEndToEnd()
    {
        await factory.ResetAsync();
        factory.EmailSender.Clear();
        const string email = "captured-registration@example.com";
        const string password = "V4lid!River-Stone-Cobalt-47";
        var cancellationToken = TestContext.Current.CancellationToken;
        var client = factory.CreateClient();

        var registration = await client.PostAsJsonAsync(
            "/api/v1/auth/register",
            new RegisterRequest { Email = email, Password = password, DisplayName = "Captured" },
            cancellationToken);
        Assert.Equal(HttpStatusCode.Accepted, registration.StatusCode);
        var message = Assert.Single(factory.EmailSender.Messages);
        Assert.Equal(email, message.To);
        var token = CapturingEmailSender.ExtractCode(message);

        var confirmation = await client.PostAsJsonAsync(
            "/api/v1/email-verification/confirm",
            new ConfirmEmailRequest { Token = token },
            cancellationToken);
        Assert.Equal(HttpStatusCode.OK, confirmation.StatusCode);

        var replay = await client.PostAsJsonAsync(
            "/api/v1/email-verification/confirm",
            new ConfirmEmailRequest { Token = token },
            cancellationToken);
        Assert.Equal(HttpStatusCode.BadRequest, replay.StatusCode);

        var loginResponse = await client.PostAsJsonAsync(
            "/api/v1/auth/login",
            new LoginRequest { Email = email, Password = password },
            cancellationToken);
        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);
        var login = await loginResponse.Content.ReadFromJsonAsync<LoginResponse>(cancellationToken);
        Assert.True(login!.User.EmailVerified);
    }

    [Fact]
    public async Task PasswordReset_UsesCapturedSingleUseTokenAndRevokesTheOldSession()
    {
        await factory.ResetAsync();
        factory.EmailSender.Clear();
        const string email = "captured-reset@example.com";
        const string oldPassword = "V4lid!River-Stone-Cobalt-47";
        const string newPassword = "N3w!River-Stone-Cobalt-84";
        var cancellationToken = TestContext.Current.CancellationToken;
        var client = factory.CreateClient();

        await client.PostAsJsonAsync(
            "/api/v1/auth/register",
            new RegisterRequest { Email = email, Password = oldPassword },
            cancellationToken);
        var verificationToken = CapturingEmailSender.ExtractCode(
            Assert.Single(factory.EmailSender.Messages));
        var verified = await client.PostAsJsonAsync(
            "/api/v1/email-verification/confirm",
            new ConfirmEmailRequest { Token = verificationToken },
            cancellationToken);
        Assert.Equal(HttpStatusCode.OK, verified.StatusCode);

        var loginResponse = await client.PostAsJsonAsync(
            "/api/v1/auth/login",
            new LoginRequest { Email = email, Password = oldPassword },
            cancellationToken);
        var login = await loginResponse.Content.ReadFromJsonAsync<LoginResponse>(cancellationToken);
        Assert.NotNull(login?.RefreshToken);

        factory.EmailSender.Clear();
        var requested = await client.PostAsJsonAsync(
            "/api/v1/password-reset/request",
            new PasswordResetRequest { Email = email },
            cancellationToken);
        Assert.Equal(HttpStatusCode.Accepted, requested.StatusCode);
        var resetMessage = Assert.Single(factory.EmailSender.Messages);
        var resetToken = CapturingEmailSender.ExtractCode(resetMessage);

        var confirmed = await client.PostAsJsonAsync(
            "/api/v1/password-reset/confirm",
            new PasswordResetConfirmRequest { Token = resetToken, NewPassword = newPassword },
            cancellationToken);
        Assert.Equal(HttpStatusCode.NoContent, confirmed.StatusCode);
        Assert.Contains(
            factory.EmailSender.Messages,
            message => message.Subject == "Security notification: Password reset completed"
                       && message.To == email
                       && !message.HtmlBody.Contains(newPassword, StringComparison.Ordinal));

        var tokenReplay = await client.PostAsJsonAsync(
            "/api/v1/password-reset/confirm",
            new PasswordResetConfirmRequest { Token = resetToken, NewPassword = newPassword },
            cancellationToken);
        Assert.Equal(HttpStatusCode.BadRequest, tokenReplay.StatusCode);

        var oldRefresh = await client.PostAsJsonAsync(
            "/api/v1/auth/refresh",
            new RefreshRequest { RefreshToken = login.RefreshToken },
            cancellationToken);
        Assert.Equal(HttpStatusCode.Unauthorized, oldRefresh.StatusCode);

        var oldLogin = await client.PostAsJsonAsync(
            "/api/v1/auth/login",
            new LoginRequest { Email = email, Password = oldPassword },
            cancellationToken);
        Assert.Equal(HttpStatusCode.Unauthorized, oldLogin.StatusCode);
        var newLogin = await client.PostAsJsonAsync(
            "/api/v1/auth/login",
            new LoginRequest { Email = email, Password = newPassword },
            cancellationToken);
        Assert.Equal(HttpStatusCode.OK, newLogin.StatusCode);
    }
}
