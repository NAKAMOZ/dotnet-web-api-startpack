namespace Api.DTOs.SocialAuth;

/// <summary>
/// Query string the provider redirects back with, at
/// <c>GET /api/v1/auth/social/{provider}/callback</c>.
/// </summary>
/// <remarks>
/// Everything here is attacker-reachable: the endpoint is anonymous and the values arrive
/// from a redirect. <see cref="State"/> is validated for signature, expiry and replay
/// <b>before</b> <see cref="Code"/> is exchanged with the provider.
/// </remarks>
public sealed record SocialCallbackQuery
{
    /// <summary>Provider authorization code, exchanged server-side with the client secret.</summary>
    public string? Code { get; init; }

    /// <summary>Signed, single-use state minted by the authorize step.</summary>
    public string? State { get; init; }

    /// <summary>Set when the user declined consent or the provider refused. Never echoed back untrusted.</summary>
    public string? Error { get; init; }
}
