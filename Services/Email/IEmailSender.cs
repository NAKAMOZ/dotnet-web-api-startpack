namespace Api.Services.Email;

public sealed record EmailMessage(string To, string Subject, string HtmlBody);

/// <summary>
/// Queues mail for asynchronous delivery. Enqueuing never waits for the SMTP provider.
/// </summary>
public interface IEmailSender
{
    ValueTask QueueAsync(EmailMessage message, CancellationToken cancellationToken);
}
