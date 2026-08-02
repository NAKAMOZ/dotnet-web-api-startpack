using Api.Data;
using Microsoft.EntityFrameworkCore;

namespace Api.Services.Email;

public sealed class SecurityNotificationService(
    AppDbContext dbContext,
    IEmailSender emailSender,
    IEmailTemplateRenderer templates,
    TimeProvider timeProvider) : ISecurityNotificationService
{
    public async Task NotifyAsync(
        Guid userId,
        string eventName,
        CancellationToken cancellationToken)
    {
        var email = await dbContext.Users
            .AsNoTracking()
            .Where(user => user.Id == userId)
            .Select(user => user.Email)
            .SingleAsync(cancellationToken);
        var occurredAt = timeProvider.GetUtcNow();

        await emailSender.QueueAsync(
            new EmailMessage(
                email,
                $"Security notification: {eventName}",
                templates.Render("SecurityNotification", new Dictionary<string, string>
                {
                    ["Event"] = eventName,
                    ["OccurredAt"] = occurredAt.ToString("O", System.Globalization.CultureInfo.InvariantCulture),
                })),
            cancellationToken);
    }
}
