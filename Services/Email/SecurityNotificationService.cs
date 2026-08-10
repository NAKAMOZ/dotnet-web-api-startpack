using Api.Data;

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
        // FindAsync rather than a projection: most callers are feature services that have
        // already loaded and mutated this user in the same scoped context, so the change
        // tracker answers without a round trip. An AsNoTracking projection deliberately
        // bypasses it and would add a SELECT to every credential change.
        var user = await dbContext.Users.FindAsync([userId], cancellationToken)
                   ?? throw new InvalidOperationException(
                       $"Cannot notify user '{userId}': the user no longer exists.");
        var email = user.Email;
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
