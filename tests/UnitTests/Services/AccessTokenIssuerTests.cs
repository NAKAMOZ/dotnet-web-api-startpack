using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Api.Configuration;
using Api.Models.Enums;
using Api.Services.Tokens;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;

namespace UnitTests.Services;

public class AccessTokenIssuerTests
{
    private static readonly DateTimeOffset Now = new(2026, 7, 26, 15, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task IssueAsync_ProducesThePinnedHeaderAndCompleteClaimSet()
    {
        using var signer = new TestSigningKeyManager();
        var issuer = CreateIssuer(signer);
        var authenticatedAt = Now.AddMinutes(-4);
        var request = Request(authenticatedAt);

        var issued = await issuer.IssueAsync(request, TestContext.Current.CancellationToken);
        var (header, payload, _) = Decode(issued.Value);

        Assert.Equal("ES256", header.GetProperty("alg").GetString());
        Assert.Equal("JWT", header.GetProperty("typ").GetString());
        Assert.Equal(TestSigningKeyManager.KeyId, header.GetProperty("kid").GetString());

        Assert.Equal("https://issuer.example", payload.GetProperty("iss").GetString());
        Assert.Equal("api-audience", payload.GetProperty("aud").GetString());
        Assert.Equal(request.UserId.ToString(), payload.GetProperty("sub").GetString());
        Assert.Equal(request.SessionId.ToString(), payload.GetProperty("sid").GetString());
        Assert.Equal(Now.ToUnixTimeSeconds(), payload.GetProperty("iat").GetInt64());
        Assert.Equal(Now.AddMinutes(15).ToUnixTimeSeconds(), payload.GetProperty("exp").GetInt64());
        Assert.Equal(authenticatedAt.ToUnixTimeSeconds(), payload.GetProperty("auth_time").GetInt64());
        Assert.Equal("access", payload.GetProperty("token_use").GetString());
        Assert.True(payload.GetProperty("email_verified").GetBoolean());
        Assert.Equal(["admin", "user"], payload.GetProperty("roles").EnumerateArray().Select(item => item.GetString()));
        Assert.Equal(
            ["pwd", "otp", "recovery", "webauthn", "google", "github"],
            payload.GetProperty("amr").EnumerateArray().Select(item => item.GetString()));
        Assert.Equal(Now.AddMinutes(15), issued.ExpiresAt);
        Assert.Equal(issued.TokenId.ToString(), payload.GetProperty("jti").GetString());
    }

    [Fact]
    public async Task IssueAsync_SignatureVerifiesAgainstThePublishedPublicKey()
    {
        using var signer = new TestSigningKeyManager();
        var issued = await CreateIssuer(signer).IssueAsync(
            Request(Now),
            TestContext.Current.CancellationToken);

        var segments = issued.Value.Split('.');
        var signingInput = Encoding.ASCII.GetBytes($"{segments[0]}.{segments[1]}");
        var signature = WebEncoders.Base64UrlDecode(segments[2]);

        Assert.True(signer.Verify(signingInput, signature));
    }

    [Fact]
    public async Task IssueAsync_WhenKeyRotatesBetweenHeaderAndSignature_RetriesWithMatchingKid()
    {
        using var signer = new TestSigningKeyManager(mismatchFirstSignature: true);
        var issued = await CreateIssuer(signer).IssueAsync(
            Request(Now),
            TestContext.Current.CancellationToken);
        var (header, _, _) = Decode(issued.Value);

        Assert.Equal(TestSigningKeyManager.KeyId, header.GetProperty("kid").GetString());
        Assert.Equal(2, signer.SignatureCount);
    }

    private static AccessTokenIssuer CreateIssuer(ISigningKeyManager signingKeyManager) =>
        new(
            signingKeyManager,
            Options.Create(new JwtOptions
            {
                Issuer = "https://issuer.example",
                Audience = "api-audience",
                AccessTokenLifetime = TimeSpan.FromMinutes(15),
                Algorithm = "ES256",
            }),
            new FakeTimeProvider(Now));

    private static AccessTokenRequest Request(DateTimeOffset authenticatedAt) => new()
    {
        UserId = Guid.Parse("01900000-0000-7000-8000-000000000010"),
        SessionId = Guid.Parse("01900000-0000-7000-8000-000000000011"),
        EmailVerified = true,
        Roles = ["admin", "user"],
        AuthenticationMethods =
        [
            AuthenticationMethod.Password,
            AuthenticationMethod.Totp,
            AuthenticationMethod.RecoveryCode,
            AuthenticationMethod.Passkey,
            AuthenticationMethod.Google,
            AuthenticationMethod.GitHub,
        ],
        AuthenticatedAt = authenticatedAt,
    };

    private static (JsonElement Header, JsonElement Payload, byte[] Signature) Decode(string token)
    {
        var segments = token.Split('.');

        return (
            JsonDocument.Parse(WebEncoders.Base64UrlDecode(segments[0])).RootElement.Clone(),
            JsonDocument.Parse(WebEncoders.Base64UrlDecode(segments[1])).RootElement.Clone(),
            WebEncoders.Base64UrlDecode(segments[2]));
    }

    private sealed class TestSigningKeyManager : ISigningKeyManager, IDisposable
    {
        public const string KeyId = "test-kid";

        private readonly ECDsa _key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        private readonly bool _mismatchFirstSignature;

        public TestSigningKeyManager(bool mismatchFirstSignature = false) =>
            _mismatchFirstSignature = mismatchFirstSignature;

        public int SignatureCount { get; private set; }

        public Task<SignatureResult> SignAsync(
            ReadOnlyMemory<byte> payload,
            CancellationToken cancellationToken)
        {
            SignatureCount++;

            var signature = _key.SignData(
                payload.Span,
                HashAlgorithmName.SHA256,
                DSASignatureFormat.IeeeP1363FixedFieldConcatenation);

            var keyId = _mismatchFirstSignature && SignatureCount == 1 ? "rotated-kid" : KeyId;
            return Task.FromResult(new SignatureResult(keyId, signature));
        }

        public Task<string> GetActiveKeyIdAsync(CancellationToken cancellationToken) =>
            Task.FromResult(KeyId);

        public Task<IReadOnlyList<PublicSigningKey>> GetPublishableKeysAsync(
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<string> RotateAsync(CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<int> RetireElapsedKeysAsync(CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<ECDsa?> ResolveValidationKeyAsync(
            string keyId,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public bool Verify(byte[] payload, byte[] signature) =>
            _key.VerifyData(
                payload,
                signature,
                HashAlgorithmName.SHA256,
                DSASignatureFormat.IeeeP1363FixedFieldConcatenation);

        public void Dispose() => _key.Dispose();
    }
}
