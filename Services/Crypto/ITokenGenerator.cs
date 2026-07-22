namespace Api.Services.Crypto;

/// <summary>
/// CSPRNG token generation and the hashing used for opaque credentials.
/// </summary>
/// <remarks>
/// Every opaque credential in this system — refresh tokens, verification tokens, MFA
/// tickets, API-key secrets, OAuth state — comes from here. One implementation means one
/// place where the entropy source, the encoding and the comparison are correct, instead of
/// six places where five of them are.
/// </remarks>
public interface ITokenGenerator
{
    /// <summary>
    /// A 256-bit cryptographically random value, base64url-encoded without padding.
    /// </summary>
    /// <remarks>
    /// 256 bits is not negotiable downward: these values are unguessable only by virtue of
    /// their entropy, since no rate limit protects a refresh-token lookup the way lockout
    /// protects a password.
    /// </remarks>
    string NewOpaqueToken();

    /// <summary>
    /// SHA-256 of a token, base64url-encoded — the form stored in the database.
    /// </summary>
    /// <remarks>
    /// A plain hash, deliberately, not Argon2id. The input is 256 bits of CSPRNG output, so
    /// there is no dictionary to attack and a work factor would only add latency to every
    /// refresh. The reasoning does <b>not</b> transfer to passwords.
    /// </remarks>
    string Hash(string token);

    /// <summary>
    /// Constant-time comparison of two token hashes.
    /// </summary>
    /// <remarks>
    /// <c>==</c> on strings returns as soon as two characters differ, so the time it takes
    /// leaks how much of a guess was right. That is enough to reconstruct a secret one
    /// character at a time.
    /// </remarks>
    bool FixedTimeEquals(string left, string right);
}
