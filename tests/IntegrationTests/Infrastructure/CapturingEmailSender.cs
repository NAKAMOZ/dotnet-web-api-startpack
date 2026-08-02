using System.Collections.Concurrent;
using System.Net;
using System.Text.RegularExpressions;
using Api.Services.Email;

namespace IntegrationTests.Infrastructure;

public sealed partial class CapturingEmailSender : IEmailSender
{
    private readonly ConcurrentQueue<EmailMessage> _messages = new();

    public IReadOnlyList<EmailMessage> Messages => [.. _messages];

    public void Clear()
    {
        while (_messages.TryDequeue(out _))
        {
        }
    }

    public ValueTask QueueAsync(EmailMessage message, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _messages.Enqueue(message);
        return ValueTask.CompletedTask;
    }

    public static string ExtractCode(EmailMessage message)
    {
        var match = CodePattern().Match(message.HtmlBody);
        return match.Success
            ? WebUtility.HtmlDecode(match.Groups[1].Value)
            : throw new InvalidOperationException("Captured email contained no <code> value.");
    }

    [GeneratedRegex("<code>([^<]+)</code>", RegexOptions.CultureInvariant)]
    private static partial Regex CodePattern();
}
