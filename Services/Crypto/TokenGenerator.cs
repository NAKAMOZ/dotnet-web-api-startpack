using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.WebUtilities;

namespace Api.Services.Crypto;

/// <inheritdoc cref="ITokenGenerator"/>
public sealed class TokenGenerator : ITokenGenerator
{
    /// <summary>256 bits. See <see cref="ITokenGenerator.NewOpaqueToken"/> for why this floor matters.</summary>
    private const int TokenBytes = 32;

    public string NewOpaqueToken() =>
        // RandomNumberGenerator, never System.Random: the latter is seeded predictably and
        // its output is reconstructable from a few samples.
        WebEncoders.Base64UrlEncode(RandomNumberGenerator.GetBytes(TokenBytes));

    public string Hash(string token) =>
        WebEncoders.Base64UrlEncode(SHA256.HashData(Encoding.UTF8.GetBytes(token)));

    public bool FixedTimeEquals(string left, string right)
    {
        var leftBytes = Encoding.UTF8.GetBytes(left);
        var rightBytes = Encoding.UTF8.GetBytes(right);

        // FixedTimeEquals returns false immediately on a length mismatch, which leaks length
        // and nothing else. That is acceptable here: these are hashes, so every value has
        // the same length, and a mismatch means malformed input rather than a near-miss.
        return CryptographicOperations.FixedTimeEquals(leftBytes, rightBytes);
    }
}
