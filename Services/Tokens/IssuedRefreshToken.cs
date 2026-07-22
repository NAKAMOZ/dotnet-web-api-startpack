namespace Api.Services.Tokens;

/// <summary>
/// A newly issued refresh token. <paramref name="Value"/> is the only moment the plaintext
/// exists — only its SHA-256 hash is persisted (ADR-0001). It must never be logged.
/// </summary>
/// <param name="Value">The opaque 256-bit value handed to the client, once.</param>
/// <param name="TokenId">Database identity, for chaining through <c>ReplacedByTokenId</c>.</param>
/// <param name="ExpiresAt">Bounded by the owning session's absolute expiry.</param>
public sealed record IssuedRefreshToken(string Value, Guid TokenId, DateTimeOffset ExpiresAt);
