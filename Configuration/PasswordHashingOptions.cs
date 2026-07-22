using System.ComponentModel.DataAnnotations;

namespace Api.Configuration;

/// <summary>
/// Argon2id cost parameters (ADR-0006).
/// </summary>
/// <remarks>
/// <b>Two profiles, and the separation is the point.</b> Passwords are human-chosen and
/// dictionary-attackable, so their hash must be slow. API keys and recovery codes are
/// machine-generated high-entropy secrets with no dictionary to attack, so a work factor
/// there buys nothing but latency on every authenticated request.
/// <para>
/// The names are deliberately unmistakable. §22 asserts the password path uses the slow
/// profile — the failure this guards against is someone noticing that API-key
/// authentication is slow and "fixing" it by sharing the fast profile.
/// </para>
/// </remarks>
public sealed class PasswordHashingOptions
{
    public const string SectionName = "PasswordHashing";

    /// <summary>
    /// Memory cost in kibibytes for user passwords. 64 MiB is the OWASP baseline.
    /// </summary>
    /// <remarks>
    /// This is a real server-side cost: every concurrent login allocates it. Tuned against
    /// production hardware in §23 — too aggressive and login becomes a denial-of-service
    /// vector against ourselves.
    /// </remarks>
    [Range(8 * 1024, 1024 * 1024)]
    public int PasswordMemoryKib { get; init; } = 64 * 1024;

    /// <summary>Iterations (time cost) for user passwords. Target ~100 ms on production hardware.</summary>
    [Range(1, 20)]
    public int PasswordIterations { get; init; } = 3;

    /// <summary>Degree of parallelism for user passwords.</summary>
    [Range(1, 16)]
    public int PasswordParallelism { get; init; } = 1;

    /// <summary>Memory cost for API keys and recovery codes. Deliberately small — see the type remarks.</summary>
    [Range(1024, 64 * 1024)]
    public int SecretMemoryKib { get; init; } = 8 * 1024;

    /// <summary>Iterations for API keys and recovery codes.</summary>
    [Range(1, 10)]
    public int SecretIterations { get; init; } = 1;

    /// <summary>Degree of parallelism for API keys and recovery codes.</summary>
    [Range(1, 16)]
    public int SecretParallelism { get; init; } = 1;

    /// <summary>Salt length in bytes.</summary>
    [Range(16, 64)]
    public int SaltLength { get; init; } = 16;

    /// <summary>Output digest length in bytes.</summary>
    [Range(16, 64)]
    public int HashLength { get; init; } = 32;
}
