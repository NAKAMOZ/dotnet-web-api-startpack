using System.Text;
using System.Text.Json;
using Api.Configuration;
using Api.Models.Enums;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Options;

namespace Api.Services.Tokens;

/// <inheritdoc cref="IAccessTokenIssuer"/>
public sealed class AccessTokenIssuer(
    ISigningKeyManager signingKeyManager,
    IOptions<JwtOptions> jwtOptions,
    TimeProvider timeProvider) : IAccessTokenIssuer
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        // A JWT is a wire format, not a document. Nulls would be sent as literal nulls and
        // change how the token parses for a strict validator.
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
    };

    private readonly JwtOptions _jwt = jwtOptions.Value;

    public async Task<IssuedAccessToken> IssueAsync(AccessTokenRequest request, CancellationToken cancellationToken)
    {
        var issuedAt = timeProvider.GetUtcNow();
        var expiresAt = issuedAt + _jwt.AccessTokenLifetime;
        var tokenId = Guid.CreateVersion7();

        var payload = new Dictionary<string, object>
        {
            ["iss"] = _jwt.Issuer,
            ["aud"] = _jwt.Audience,
            ["sub"] = request.UserId.ToString(),
            ["sid"] = request.SessionId.ToString(),
            ["jti"] = tokenId.ToString(),
            ["iat"] = issuedAt.ToUnixTimeSeconds(),
            ["exp"] = expiresAt.ToUnixTimeSeconds(),

            // When the USER last authenticated, carried forward across refreshes unchanged.
            // Not iat — iat moves forward on every rotation, so using it here would make a
            // stolen session pass step-up forever (Authentication.md §14).
            ["auth_time"] = request.AuthenticatedAt.ToUnixTimeSeconds(),

            ["email_verified"] = request.EmailVerified,
            ["roles"] = request.Roles,
            ["amr"] = request.AuthenticationMethods.Select(ToAmrValue).ToArray(),

            // Rejects a refresh token presented as a bearer token. Without it, any opaque
            // credential that happened to parse would be evaluated on its claims alone.
            ["token_use"] = "access",
        };

        // The header must carry the kid, and the header is part of what gets signed — so the
        // signing key's identity has to be known before there is anything to sign.
        var keyId = await signingKeyManager.GetActiveKeyIdAsync(cancellationToken);

        var header = new Dictionary<string, object>
        {
            // Pinned, never negotiated. A validator that reads this field to choose a
            // strategy is the algorithm-confusion vulnerability (Authentication.md §2).
            ["alg"] = _jwt.Algorithm,
            ["typ"] = "JWT",
            ["kid"] = keyId,
        };

        var signingInput = $"{EncodeSegment(header)}.{EncodeSegment(payload)}";

        var signature = await signingKeyManager.SignAsync(
            Encoding.ASCII.GetBytes(signingInput),
            cancellationToken);

        var token = $"{signingInput}.{WebEncoders.Base64UrlEncode(signature.Signature)}";

        return new IssuedAccessToken(token, tokenId, expiresAt);
    }

    /// <summary>Maps an internal enum member to its RFC 8176 <c>amr</c> value.</summary>
    private static string ToAmrValue(AuthenticationMethod method) => method switch
    {
        AuthenticationMethod.Password => "pwd",
        AuthenticationMethod.Totp => "otp",
        AuthenticationMethod.RecoveryCode => "recovery",
        AuthenticationMethod.Passkey => "webauthn",
        AuthenticationMethod.Google => "google",
        AuthenticationMethod.GitHub => "github",

        // Exhaustive by design: a new member must be given a wire value deliberately, not
        // silently serialized as its C# name.
        _ => throw new ArgumentOutOfRangeException(nameof(method), method, "No amr value defined."),
    };

    private static string EncodeSegment(Dictionary<string, object> segment) =>
        WebEncoders.Base64UrlEncode(JsonSerializer.SerializeToUtf8Bytes(segment, SerializerOptions));
}
