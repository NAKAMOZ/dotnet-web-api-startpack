namespace Api.Services.Tokens;

/// <summary>A freshly minted access token and the metadata a caller needs to transport it.</summary>
/// <param name="Value">The compact JWT.</param>
/// <param name="TokenId">The <c>jti</c> claim, for audit correlation.</param>
/// <param name="ExpiresAt">Absolute expiry. Also the upper bound on revocation lag (ADR-0001).</param>
public sealed record IssuedAccessToken(string Value, Guid TokenId, DateTimeOffset ExpiresAt);
