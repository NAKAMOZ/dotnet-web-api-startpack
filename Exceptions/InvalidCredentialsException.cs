namespace Api.Exceptions;

/// <summary>
/// Authentication failed. <b>The only exception the login path may throw for a failed
/// attempt</b> — 401, one code, one message.
/// </summary>
/// <remarks>
/// Unknown email, wrong password, locked account and passwordless account all raise this,
/// identically. Distinguishing them would hand an attacker an account-enumeration oracle,
/// and a helpful "no account with that email" is exactly the help they need
/// (Authentication.md §5).
/// <para>
/// Timing has to match too, which is why §12's login path runs a dummy Argon2id verification
/// when no user exists. Without it the "no user" branch returns in microseconds and the fast
/// path <em>is</em> the oracle, regardless of what the body says.
/// </para>
/// </remarks>
public sealed class InvalidCredentialsException()
    : DomainException("invalid_credentials", "Invalid email or password.");
