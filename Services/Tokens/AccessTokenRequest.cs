using Api.Models.Enums;

namespace Api.Services.Tokens;

/// <summary>
/// Everything needed to mint an access token. Deliberately a value object rather than a
/// long parameter list — the claim set is security-relevant and adding a claim should be
/// a visible change to this type.
/// </summary>
public sealed record AccessTokenRequest
{
    public required Guid UserId { get; init; }

    /// <summary>Session this token belongs to. Becomes the <c>sid</c> claim.</summary>
    public required Guid SessionId { get; init; }

    /// <summary>Becomes <c>email_verified</c>. Gates flows that require a verified address.</summary>
    public required bool EmailVerified { get; init; }

    /// <summary>Role names. Source for policy-based authorization (§5).</summary>
    public required IReadOnlyCollection<string> Roles { get; init; }

    /// <summary>How this session authenticated. Becomes <c>amr</c>.</summary>
    public required IReadOnlyCollection<AuthenticationMethod> AuthenticationMethods { get; init; }

    /// <summary>
    /// When the <b>user</b> last proved an authentication factor. Becomes the
    /// <c>auth_time</c> claim and drives step-up (Authentication.md §14).
    /// </summary>
    /// <remarks>
    /// This is <b>not</b> the token's <c>iat</c>. It is carried forward unchanged across
    /// refreshes, and is only advanced by a real re-authentication. Sourcing it from "now"
    /// on every issuance — the obvious-looking simplification — silently defeats step-up:
    /// a stolen session refreshes every 15 minutes, so the value would always look recent
    /// for exactly the attacker the control exists to stop.
    /// </remarks>
    public required DateTimeOffset AuthenticatedAt { get; init; }
}
