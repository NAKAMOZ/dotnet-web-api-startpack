namespace Api.DTOs.Mfa;

/// <summary>
/// A freshly generated batch of MFA recovery codes — returned by TOTP confirmation and by
/// <c>POST /api/v1/mfa/recovery-codes/regenerate</c>.
/// </summary>
/// <remarks>
/// <b>Shown once.</b> Only hashes are stored, so there is no endpoint that can list them
/// again; a user who loses them regenerates, which invalidates the previous batch.
/// <para>
/// Regeneration is step-up protected (Authorization.md §6) for that reason: it silently
/// invalidates codes the user may have printed and stored, and an attacker on a stolen
/// session would use it to lock the real owner out of their own fallback.
/// </para>
/// </remarks>
public sealed record RecoveryCodesResponse
{
    /// <summary>Plaintext codes, this once. Never logged.</summary>
    public required IReadOnlyList<string> Codes { get; init; }

    public required DateTimeOffset GeneratedAt { get; init; }
}
