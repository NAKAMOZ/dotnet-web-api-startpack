namespace Api.Services.Auth;

public interface ILogoutService
{
    Task LogoutAsync(Guid userId, Guid sessionId, CancellationToken cancellationToken);
}
