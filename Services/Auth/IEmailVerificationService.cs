using Api.DTOs.EmailVerification;

namespace Api.Services.Auth;

public interface IEmailVerificationService
{
    Task SendAsync(Guid userId, CancellationToken cancellationToken);

    Task<EmailVerifiedResponse> ConfirmAsync(string token, CancellationToken cancellationToken);
}
