namespace Api.Logging;

/// <summary>
/// The one place that decides whether a field name looks like a secret (ADR-0010's
/// never-logged list).
/// </summary>
/// <remarks>
/// Two consumers, deliberately sharing one list: <see cref="SensitiveDataDestructuringPolicy"/>
/// for anything reaching a log sink, and <c>AuditMetadataSerializer</c> for anything reaching
/// the <c>Metadata</c> column of the audit table. The audit table is the trap — it is durable,
/// exempt from log rotation, and the last place anyone thinks to look for a leaked credential.
/// Two copies of this list would agree until one of them was extended.
/// <para>
/// Same reasoning as <c>PasswordRules</c>: a policy that exists in two files is a policy that
/// will hold in one of them.
/// </para>
/// </remarks>
public static class SensitiveFieldNames
{
    /// <summary>What a redacted value is replaced with, in logs and in audit metadata alike.</summary>
    public const string RedactedValue = "[redacted]";

    /// <summary>
    /// Substrings that mark a field as a secret, matched case-insensitively. Covers ADR-0010's
    /// list plus the names this model uses for stored derivatives — <c>PasswordHash</c>,
    /// <c>TokenHash</c>, <c>KeyHash</c>, <c>SecretEncrypted</c>, <c>CodeHash</c>.
    /// </summary>
    /// <remarks>
    /// Substring matching over-redacts: a field called <c>HashAlgorithm</c> disappears with
    /// the hashes. That is the direction to be wrong in — the cost is one missing diagnostic
    /// field, and the cost of the other direction is a credential in durable storage.
    /// </remarks>
    private static readonly string[] SecretFragments =
    [
        "password",
        "token",
        "secret",
        "hash",
        "apikey",
        "credential",
        "cookie",
        "authorization",
        "recoverycode",
        "privatekey",
        "signature",
    ];

    private static readonly string[] EmailFragments = ["email"];

    /// <summary>Whether a field of this name must never have its value recorded.</summary>
    public static bool IsSecret(string fieldName) => Matches(fieldName, SecretFragments);

    /// <summary>
    /// Whether a field of this name holds an email address, which is masked rather than
    /// redacted.
    /// </summary>
    /// <remarks>
    /// The audit table is the documented exception (§15): rows there store the address in
    /// full, because "which account was attacked" is the question the trail exists to answer.
    /// That exception applies to the typed columns, not to free-form <c>Metadata</c>.
    /// </remarks>
    public static bool IsEmail(string fieldName) => Matches(fieldName, EmailFragments);

    /// <summary>
    /// <c>nevzat@example.com</c> → <c>n***@example.com</c>.
    /// </summary>
    /// <remarks>
    /// The domain survives because it is the operationally useful half — "a hundred failed
    /// logins, all at one domain" is a finding, and a domain is not personal data. One leading
    /// character survives so a support conversation can confirm an address the caller reads
    /// out, without the log line holding it. A single-character local part is masked whole,
    /// since revealing it would reveal the address.
    /// </remarks>
    public static string MaskEmail(string value)
    {
        var atIndex = value.IndexOf('@', StringComparison.Ordinal);

        // Not an address after all — a field named *Email* holding something else. Redact
        // rather than mask: the masking rule assumes a shape this value does not have, and
        // applying it anyway would reveal an arbitrary prefix of an unknown string.
        if (atIndex <= 0 || atIndex == value.Length - 1)
        {
            return RedactedValue;
        }

        var leadingCharacter = atIndex == 1 ? "*" : value[..1];

        return string.Concat(leadingCharacter, "***", value.AsSpan(atIndex));
    }

    private static bool Matches(string fieldName, string[] fragments) =>
        fragments.Any(fragment => fieldName.Contains(fragment, StringComparison.OrdinalIgnoreCase));
}
