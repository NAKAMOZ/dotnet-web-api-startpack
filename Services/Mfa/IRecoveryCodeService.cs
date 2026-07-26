using Api.DTOs.Mfa;

namespace Api.Services.Mfa;

public interface IRecoveryCodeService
{
    Task<RecoveryCodesResponse> RegenerateAsync(
        Guid userId,
        CancellationToken cancellationToken);
}
