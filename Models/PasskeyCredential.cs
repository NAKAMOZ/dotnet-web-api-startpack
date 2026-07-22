namespace Api.Models;

/// <summary>
/// A registered WebAuthn/FIDO2 credential. Nothing stored here is secret — the private key
/// never leaves the authenticator, which is the property that makes passkeys unphishable.
/// </summary>
public sealed class PasskeyCredential : IAuditableEntity
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    public Guid UserId { get; set; }

    public User User { get; set; } = null!;

    /// <summary>
    /// The authenticator's credential ID. Unique across all users (§7), and the lookup key
    /// during an assertion.
    /// </summary>
    /// <remarks>
    /// Deletion by this id must be scoped to the caller in the same query
    /// (<c>WHERE CredentialId = @id AND UserId = @sub</c>). Fetch-then-compare is the
    /// classic IDOR shape: it works until the comparison is refactored away, and it leaks
    /// existence through timing even while it works (Authorization.md §5).
    /// </remarks>
    public required byte[] CredentialId { get; set; }

    /// <summary>COSE-encoded public key. Public by definition — this is what verifies assertions.</summary>
    public required byte[] PublicKey { get; set; }

    /// <summary>
    /// The authenticator's signature counter, as of the last assertion.
    /// </summary>
    /// <remarks>
    /// Stored as a 64-bit integer for a 32-bit WebAuthn value so arithmetic near
    /// <c>uint.MaxValue</c> cannot wrap. A counter that fails to advance is the
    /// cloned-authenticator signal: it means two devices are answering for one credential.
    /// It is audited, not silently accepted — but note that authenticators which always
    /// report zero are legal, so zero is not by itself a regression.
    /// </remarks>
    public long SignCount { get; set; }

    /// <summary>Authenticator model identifier. Null-GUID when the authenticator withholds it.</summary>
    public Guid Aaguid { get; set; }

    /// <summary>Transports the authenticator reported (<c>usb</c>, <c>nfc</c>, <c>ble</c>, <c>internal</c>, <c>hybrid</c>).</summary>
    public ICollection<string> Transports { get; set; } = [];

    /// <summary>User-supplied name, so the credential list is readable ("YubiKey", "iPhone").</summary>
    public string? Label { get; set; }

    public DateTimeOffset? LastUsedAt { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }
}
