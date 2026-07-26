using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Api.Services.Tokens;
using IntegrationTests.Infrastructure;
using Microsoft.AspNetCore.WebUtilities;

namespace IntegrationTests.Security;

[Collection(IntegrationTestCollection.Name)]
[Trait("Category", "Security")]
public sealed class JwtAttackTests(IntegrationTestFactory factory)
{
    [Theory]
    [InlineData("alg-none")]
    [InlineData("algorithm-confusion")]
    [InlineData("tampered-payload")]
    [InlineData("wrong-issuer")]
    [InlineData("wrong-audience")]
    [InlineData("unknown-kid")]
    public async Task JwtAttack_IsRejected(string attack)
    {
        await factory.ResetAsync();
        var token = await IssueTokenAsync();
        var malicious = await ApplyAttackAsync(token, attack);

        var response = await SendToProtectedEndpointAsync(malicious);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task ExpiredToken_IsRejectedAfterAdvancingTheSharedClock()
    {
        await factory.ResetAsync();
        var token = await IssueTokenAsync();

        factory.Clock.Advance(TimeSpan.FromMinutes(16));

        var response = await SendToProtectedEndpointAsync(token);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task RetiringKey_ValidatesUntilGraceThenRetiredKidIsRejected()
    {
        await factory.ResetAsync();
        var oldToken = await IssueTokenAsync();

        await factory.InScopeAsync(async services =>
        {
            var keys = services.GetRequiredService<ISigningKeyManager>();
            await keys.RotateAsync(TestContext.Current.CancellationToken);
        });

        var duringGrace = await SendToProtectedEndpointAsync(oldToken);
        Assert.Equal(HttpStatusCode.OK, duringGrace.StatusCode);

        var jwks = await factory.CreateClient().GetFromJsonAsync<JsonObject>(
            "/.well-known/jwks.json",
            TestContext.Current.CancellationToken);
        Assert.Equal(2, jwks!["keys"]!.AsArray().Count);

        factory.Clock.Advance(TimeSpan.FromMinutes(21));
        var retiredCount = await factory.InScopeAsync(services =>
            services.GetRequiredService<ISigningKeyManager>().RetireElapsedKeysAsync(
                TestContext.Current.CancellationToken));

        Assert.Equal(1, retiredCount);

        var afterGrace = await SendToProtectedEndpointAsync(oldToken);
        Assert.Equal(HttpStatusCode.Unauthorized, afterGrace.StatusCode);
    }

    private async Task<string> IssueTokenAsync()
    {
        var userId = await factory.SeedUserAsync(TestContext.Current.CancellationToken);

        return await factory.IssueAccessTokenAsync(
            userId,
            Guid.CreateVersion7(),
            TestContext.Current.CancellationToken);
    }

    private async Task<string> ApplyAttackAsync(string token, string attack)
    {
        var segments = token.Split('.');
        var header = JsonNode.Parse(WebEncoders.Base64UrlDecode(segments[0]))!.AsObject();
        var payload = JsonNode.Parse(WebEncoders.Base64UrlDecode(segments[1]))!.AsObject();

        switch (attack)
        {
            case "alg-none":
                header["alg"] = "none";
                return $"{Encode(header)}.{Encode(payload)}.";

            case "algorithm-confusion":
                header["alg"] = "HS256";
                var input = $"{Encode(header)}.{Encode(payload)}";
                var publicKey = await PublicKeyBytesAsync();
                using (var hmac = new HMACSHA256(publicKey))
                {
                    return $"{input}.{WebEncoders.Base64UrlEncode(
                        hmac.ComputeHash(Encoding.ASCII.GetBytes(input)))}";
                }

            case "tampered-payload":
                payload["sub"] = Guid.CreateVersion7().ToString();
                return $"{Encode(header)}.{Encode(payload)}.{segments[2]}";

            case "wrong-issuer":
                payload["iss"] = "https://attacker.invalid";
                return await SignAsync(header, payload);

            case "wrong-audience":
                payload["aud"] = "another-api";
                return await SignAsync(header, payload);

            case "unknown-kid":
                header["kid"] = "unknown-key";
                return await SignAsync(header, payload);

            default:
                throw new ArgumentOutOfRangeException(nameof(attack), attack, "Unknown JWT attack.");
        }
    }

    private async Task<string> SignAsync(JsonObject header, JsonObject payload)
    {
        var input = $"{Encode(header)}.{Encode(payload)}";
        var signature = await factory.InScopeAsync(services =>
            services.GetRequiredService<ISigningKeyManager>().SignAsync(
                Encoding.ASCII.GetBytes(input),
                TestContext.Current.CancellationToken));

        return $"{input}.{WebEncoders.Base64UrlEncode(signature.Signature)}";
    }

    private async Task<byte[]> PublicKeyBytesAsync()
    {
        var key = (await factory.InScopeAsync(services =>
            services.GetRequiredService<ISigningKeyManager>().GetPublishableKeysAsync(
                TestContext.Current.CancellationToken))).Single();

        using var ecdsa = ECDsa.Create(new ECParameters
        {
            Curve = ECCurve.NamedCurves.nistP256,
            Q = new ECPoint
            {
                X = WebEncoders.Base64UrlDecode(key.X),
                Y = WebEncoders.Base64UrlDecode(key.Y),
            },
        });

        return ecdsa.ExportSubjectPublicKeyInfo();
    }

    private async Task<HttpResponseMessage> SendToProtectedEndpointAsync(string token)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return await client.GetAsync(
            "/api/v1/users/me",
            TestContext.Current.CancellationToken);
    }

    private static string Encode(JsonObject value) =>
        WebEncoders.Base64UrlEncode(JsonSerializer.SerializeToUtf8Bytes(value));
}
