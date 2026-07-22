namespace Api.Services.Tokens;

/// <summary>
/// A short-lived ticket proving the password step succeeded. It is a credential in its own
/// right — hashed at rest, single-use — and authorises exactly one thing: completing this
/// login. <paramref name="Value"/> must never be logged.
/// </summary>
/// <param name="Value">The opaque plaintext, returned to the client once.</param>
/// <param name="ExpiresAt">Five minutes out, per <c>AuthSessionOptions.MfaTicketLifetime</c>.</param>
public sealed record IssuedMfaTicket(string Value, DateTimeOffset ExpiresAt);
