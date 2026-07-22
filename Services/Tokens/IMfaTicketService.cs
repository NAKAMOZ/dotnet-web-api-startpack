namespace Api.Services.Tokens;

/// <summary>
/// Issues and consumes the tickets that bridge the two halves of an MFA login.
/// </summary>
/// <remarks>
/// Implemented in §12. Contract specified in Authentication.md §8. Tickets are stored as
/// <c>VerificationToken</c> rows of type <c>MfaChallenge</c>.
/// </remarks>
public interface IMfaTicketService
{
    /// <summary>Issues a ticket after a successful password step, when TOTP is enrolled.</summary>
    Task<IssuedMfaTicket> IssueAsync(Guid userId, CancellationToken cancellationToken);

    /// <summary>
    /// Validates and atomically consumes a ticket.
    /// </summary>
    /// <remarks>
    /// Consumption must be atomic with validation. A check-then-consume gap lets two
    /// concurrent requests both pass validation and complete the same login twice.
    /// </remarks>
    /// <returns>The user id, or <see langword="null"/> if the ticket is unknown, expired or already consumed.</returns>
    Task<Guid?> ConsumeAsync(string presentedTicket, CancellationToken cancellationToken);
}
