using Api.DTOs.Mfa;
using Api.Models.Enums;
using Api.Services.Mfa;
using OtpNet;

namespace IntegrationTests;

[Collection(IntegrationTestCollection.Name)]
public sealed class MfaServiceIntegrationTests(IntegrationTestFactory factory)
{
    [Fact]
    public async Task TotpAndRecoveryCodes_AreSingleUseIncludingConcurrentReplay()
    {
        await factory.ResetAsync();
        var cancellationToken = TestContext.Current.CancellationToken;
        var userId = await factory.SeedUserAsync(cancellationToken);

        var enrollment = await factory.InScopeAsync(services =>
            services.GetRequiredService<ITotpService>().EnrollAsync(userId, cancellationToken));
        var totp = new Totp(Base32Encoding.ToBytes(enrollment.Secret));
        var confirmationCode = totp.ComputeTotp(factory.Clock.GetUtcNow().UtcDateTime);
        var recoveryCodes = await factory.InScopeAsync(services =>
            services.GetRequiredService<ITotpService>().ConfirmAsync(
                userId,
                confirmationCode,
                cancellationToken));

        // Confirmation consumes its TOTP step; the enrollment code cannot immediately be
        // replayed as a login factor.
        Assert.Null(await VerifyAsync(confirmationCode));

        factory.Clock.Advance(TimeSpan.FromSeconds(30));
        var firstLoginCode = totp.ComputeTotp(factory.Clock.GetUtcNow().UtcDateTime);
        Assert.Equal(AuthenticationMethod.Totp, await VerifyAsync(firstLoginCode));
        Assert.Null(await VerifyAsync(firstLoginCode));

        factory.Clock.Advance(TimeSpan.FromSeconds(30));
        var concurrentCode = totp.ComputeTotp(factory.Clock.GetUtcNow().UtcDateTime);
        var concurrentResults = await Task.WhenAll(VerifyAsync(concurrentCode), VerifyAsync(concurrentCode));
        Assert.Equal(1, concurrentResults.Count(result => result == AuthenticationMethod.Totp));
        Assert.Equal(1, concurrentResults.Count(result => result is null));

        var recoveryCode = recoveryCodes.Codes[0];
        Assert.Equal(AuthenticationMethod.RecoveryCode, await VerifyAsync(recoveryCode));
        Assert.Null(await VerifyAsync(recoveryCode));

        Task<AuthenticationMethod?> VerifyAsync(string code) =>
            factory.InScopeAsync(services =>
                services.GetRequiredService<ITotpService>().VerifyAsync(userId, code, cancellationToken));
    }
}
