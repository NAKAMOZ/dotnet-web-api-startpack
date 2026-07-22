namespace Api.Exceptions;

/// <summary>
/// Registration hit an existing address.
/// </summary>
/// <remarks>
/// ⚠️ <b>Whether this may reach the client is an open decision</b> (§11's owner question).
/// Surfacing it as a 409 tells an anonymous caller which addresses are registered — the same
/// oracle the password-reset flow deliberately refuses to provide. The alternative is for
/// registration to always answer <c>202</c> and send either a welcome email or a "someone
/// tried to register your address" notice.
/// <para>
/// The type exists either way: even under the 202 design, the service still needs to
/// distinguish the two cases internally to decide which email to send and what to audit.
/// </para>
/// </remarks>
public sealed class EmailAlreadyRegisteredException()
    : DomainException("email_already_registered", "That email address is already registered.");
