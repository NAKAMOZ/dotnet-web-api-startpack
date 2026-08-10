namespace Api.Configuration;

/// <summary>Stable names used by endpoint metadata, configuration, tests and documentation.</summary>
public static class RateLimitPolicies
{
    public const string AuthStrict = "auth-strict";
    public const string EmailSending = "email-sending";
    public const string Registration = "registration";
    public const string General = "general";

    /// <summary>
    /// The per-target-account half of <see cref="EmailSending"/>, enforced by
    /// <c>EmailTargetRateLimitFilter</c> rather than by endpoint metadata. It is named here
    /// with the others because the distributed limiter uses the policy name as the prefix of a
    /// durable Redis key: renaming it re-namespaces every live counter mid-flight.
    /// </summary>
    public const string EmailSendingAccount = "email-sending-account";
}
