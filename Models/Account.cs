namespace Api.Models;

/// <summary>
/// A link between a local <see cref="User"/> and an external identity provider
/// (ADR-0019: Google and GitHub in v1).
/// </summary>
/// <remarks>
/// Provider access and refresh tokens are deliberately <b>not</b> stored. Nothing in v1
/// calls a provider API on the user's behalf, so keeping them would be a standing breach
/// liability bought with no capability. A feature that needs them adds the columns, encrypted,
/// with an ADR.
/// </remarks>
public sealed class Account : IAuditableEntity
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    public Guid UserId { get; set; }

    public User User { get; set; } = null!;

    /// <summary>Provider key — <c>google</c> or <c>github</c>. Lower-case, from a fixed set.</summary>
    public required string Provider { get; set; }

    /// <summary>
    /// The provider's stable subject identifier. Unique together with
    /// <see cref="Provider"/> (§7).
    /// </summary>
    /// <remarks>
    /// <b>This pair is the only thing an account may be matched on.</b> Matching an
    /// incoming social login to an existing user by email address instead would hand any
    /// account to whoever can get a provider to assert its address — which is exactly what
    /// an unverified provider email lets an attacker do (Authentication.md §9).
    /// </remarks>
    public required string ProviderAccountId { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }
}
