using System.Diagnostics;
using Api.Configuration;
using Api.Logging;
using Isopoh.Cryptography.Argon2;
using Microsoft.Extensions.Options;

namespace Api.Services.Crypto;

/// <inheritdoc cref="IPasswordHasher"/>
public sealed class Argon2PasswordHasher(
    IOptions<PasswordHashingOptions> options,
    AuthMetrics metrics) : IPasswordHasher
{
    private readonly PasswordHashingOptions _options = options.Value;

    public string Hash(string password)
    {
        var started = Stopwatch.GetTimestamp();

        try
        {
            return Argon2.Hash(
                password,
                timeCost: _options.PasswordIterations,
                memoryCost: _options.PasswordMemoryKib,
                parallelism: _options.PasswordParallelism,
                type: Argon2Type.HybridAddressing,
                hashLength: _options.HashLength);
        }
        finally
        {
            metrics.RecordPasswordHashDuration(
                Stopwatch.GetElapsedTime(started),
                "hash");
        }
    }

    /// <summary>
    /// Hashes a machine-generated secret — an API key or a recovery code — with the cheap
    /// profile.
    /// </summary>
    /// <remarks>
    /// Separate method rather than a boolean parameter. A parameter defaults, and a
    /// defaulted parameter eventually gets the password path wrong; a differently named
    /// method cannot be reached by accident, and shows up in review as itself.
    /// </remarks>
    public string HashSecret(string secret) =>
        Argon2.Hash(
            secret,
            timeCost: _options.SecretIterations,
            memoryCost: _options.SecretMemoryKib,
            parallelism: _options.SecretParallelism,
            type: Argon2Type.HybridAddressing,
            hashLength: _options.HashLength);

    public bool Verify(string password, string hash)
    {
        var started = Stopwatch.GetTimestamp();

        // A malformed stored value is an authentication failure, not a 500. Throwing here
        // would tell a caller their guess reached something unusual — and would turn one
        // corrupt row into an outage for that account.
        try
        {
            return Argon2.Verify(hash, password);
        }
        catch (Exception exception) when (exception is FormatException or ArgumentException)
        {
            return false;
        }
        finally
        {
            metrics.RecordPasswordHashDuration(
                Stopwatch.GetElapsedTime(started),
                "verify");
        }
    }

    /// <summary>
    /// Whether a stored hash was produced with weaker parameters than the current
    /// configuration.
    /// </summary>
    /// <remarks>
    /// Reads the parameters out of the encoded hash — <c>$argon2id$v=19$m=65536,t=3,p=1$…</c>
    /// — rather than from configuration. That is what makes gradual migration possible:
    /// raising the cost does not invalidate a single existing hash, it just marks them for
    /// re-hashing on next login, when the plaintext is briefly available and at no other time.
    /// <para>
    /// An unparseable hash returns <see langword="true"/>: if we cannot tell how it was made,
    /// re-hashing it with current parameters is the safe answer.
    /// </para>
    /// </remarks>
    public bool NeedsRehash(string hash)
    {
        var parameters = ParseParameters(hash);

        if (parameters is not var (memory, iterations, parallelism))
        {
            return true;
        }

        return memory < _options.PasswordMemoryKib
               || iterations < _options.PasswordIterations
               || parallelism < _options.PasswordParallelism;
    }

    private static (int Memory, int Iterations, int Parallelism)? ParseParameters(string hash)
    {
        // $argon2id$v=19$m=65536,t=3,p=1$<salt>$<digest>
        var segments = hash.Split('$');

        if (segments.Length < 4)
        {
            return null;
        }

        int memory = 0, iterations = 0, parallelism = 0;

        foreach (var pair in segments[3].Split(','))
        {
            var parts = pair.Split('=');

            if (parts.Length != 2 || !int.TryParse(parts[1], out var value))
            {
                return null;
            }

            switch (parts[0])
            {
                case "m": memory = value; break;
                case "t": iterations = value; break;
                case "p": parallelism = value; break;
                default: return null;
            }
        }

        return memory > 0 && iterations > 0 && parallelism > 0
            ? (memory, iterations, parallelism)
            : null;
    }
}
