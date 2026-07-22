using System.ComponentModel.DataAnnotations;

namespace Api.Configuration;

/// <summary>
/// Access-token issuance and validation settings.
/// See <c>Documentation/Architecture/Authentication.md</c> §2.
/// </summary>
public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    /// <summary>Expected <c>iss</c> claim. Validated strictly on every token.</summary>
    [Required(AllowEmptyStrings = false)]
    public string Issuer { get; init; } = string.Empty;

    /// <summary>Expected <c>aud</c> claim. Validated strictly on every token.</summary>
    [Required(AllowEmptyStrings = false)]
    public string Audience { get; init; } = string.Empty;

    /// <summary>
    /// Access-token lifetime. 15 minutes is not a tuning knob — it is the bound on how
    /// long a revoked session's access token stays valid (ADR-0001). Raising it widens
    /// that window by exactly the same amount.
    /// </summary>
    [Range(typeof(TimeSpan), "00:01:00", "01:00:00")]
    public TimeSpan AccessTokenLifetime { get; init; } = TimeSpan.FromMinutes(15);

    /// <summary>
    /// Tolerance applied to <c>exp</c> and <c>nbf</c>. Deliberately 30 seconds rather
    /// than the framework default of 5 minutes, which would extend every access token's
    /// effective life by a third.
    /// </summary>
    [Range(typeof(TimeSpan), "00:00:00", "00:05:00")]
    public TimeSpan ClockSkew { get; init; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// The only accepted signing algorithm. Validation pins this value; the algorithm is
    /// never read from the token header to select a strategy — that is the
    /// algorithm-substitution vulnerability (§22 tests it).
    /// </summary>
    public string Algorithm { get; init; } = "ES256";

    /// <summary>
    /// How long a demoted key stays in <c>Retiring</c> before it may be retired. Must be
    /// at least <see cref="AccessTokenLifetime"/> + <see cref="ClockSkew"/>, or tokens
    /// still legitimately in flight stop validating (ADR-0004).
    /// </summary>
    public TimeSpan KeyRetirementGrace { get; init; } = TimeSpan.FromMinutes(20);
}
