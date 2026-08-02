using System.Net;
using System.Net.Mail;
using System.Threading.Channels;
using Api.Configuration;
using Microsoft.Extensions.Options;

namespace Api.Services.Email;

/// <summary>
/// Bounded, in-process SMTP delivery queue. Request paths only enqueue; provider latency and
/// failures are deliberately unobservable to callers.
/// </summary>
public sealed class SmtpEmailSender(
    IOptions<EmailOptions> options,
    ILogger<SmtpEmailSender> logger) : BackgroundService, IEmailSender
{
    private readonly EmailOptions _options = options.Value;
    private readonly Channel<EmailMessage> _messages = Channel.CreateBounded<EmailMessage>(
        new BoundedChannelOptions(1_000)
        {
            // TryWrite below makes saturation observable and preserves already-queued mail.
            // DropOldest lets an attacker evict a legitimate verification/reset message.
            FullMode = BoundedChannelFullMode.Wait,
            SingleReader = true,
            SingleWriter = false,
        });

    public ValueTask QueueAsync(EmailMessage message, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!_messages.Writer.TryWrite(message))
        {
            logger.LogCritical(
                "Email delivery queue is full; a message was not queued. No recipient or body metadata is logged.");
        }

        return ValueTask.CompletedTask;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var message in _messages.Reader.ReadAllAsync(stoppingToken))
        {
            try
            {
                using var mail = new MailMessage(_options.FromAddress, message.To, message.Subject, message.HtmlBody)
                {
                    IsBodyHtml = true,
                };
                using var smtp = new SmtpClient(_options.Host, _options.Port)
                {
                    EnableSsl = _options.UseTls,
                };

                if (!string.IsNullOrWhiteSpace(_options.Username))
                {
                    smtp.Credentials = new NetworkCredential(_options.Username, _options.Password);
                }

                await smtp.SendMailAsync(mail, stoppingToken);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                logger.LogError(exception, "Email delivery failed for queued message.");
            }
        }
    }
}
