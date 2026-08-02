namespace Api.Services.Email;

public interface ISecurityNotificationService
{
    Task NotifyAsync(Guid userId, string eventName, CancellationToken cancellationToken);
}
