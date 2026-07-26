using Api.DTOs.Mfa;
using Api.Models.Enums;

namespace Api.Services.Mfa;

public interface ITotpService
{
    Task<TotpEnrollmentResponse> EnrollAsync(Guid userId, CancellationToken cancellationToken);

    Task<RecoveryCodesResponse> ConfirmAsync(
        Guid userId,
        string code,
        CancellationToken cancellationToken);

    Task<AuthenticationMethod?> VerifyAsync(
        Guid userId,
        string code,
        CancellationToken cancellationToken);

    Task DisableAsync(Guid userId, CancellationToken cancellationToken);

    Task<RecoveryCodesResponse> RegenerateRecoveryCodesAsync(
        Guid userId,
        CancellationToken cancellationToken);
}
