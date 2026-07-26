using Api.DTOs.PasswordReset;

namespace Api.Services.Auth;

public interface IPasswordResetService
{
    Task RequestAsync(PasswordResetRequest request, CancellationToken cancellationToken);

    Task ConfirmAsync(PasswordResetConfirmRequest request, CancellationToken cancellationToken);
}
