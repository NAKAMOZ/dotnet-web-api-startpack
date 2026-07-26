using Api.DTOs.Mfa;

namespace Api.Services.Mfa;

public sealed class RecoveryCodeService(ITotpService totpService) : IRecoveryCodeService
{
    public Task<RecoveryCodesResponse> RegenerateAsync(
        Guid userId,
        CancellationToken cancellationToken) =>
        totpService.RegenerateRecoveryCodesAsync(userId, cancellationToken);
}
