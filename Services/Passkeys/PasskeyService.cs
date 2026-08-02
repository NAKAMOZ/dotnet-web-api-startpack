using System.Text.Json;
using Api.Configuration;
using Api.Data;
using Api.DTOs.Auth;
using Api.DTOs.Passkeys;
using Api.Exceptions;
using Api.Models;
using Api.Models.Enums;
using Api.Services.Audit;
using Api.Services.Auth;
using Api.Services.Crypto;
using Api.Services.Email;
using Fido2NetLib;
using Fido2NetLib.Objects;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Api.Services.Passkeys;

public sealed class PasskeyService(
    AppDbContext dbContext,
    IFido2 fido2,
    ITokenGenerator tokenGenerator,
    IAuthenticationSessionFactory sessionFactory,
    IAuditLogger auditLogger,
    ISecurityNotificationService securityNotifications,
    IOptions<AuthSessionOptions> sessionOptions,
    TimeProvider timeProvider) : IPasskeyService
{
    private readonly AuthSessionOptions _session = sessionOptions.Value;

    public async Task<PasskeyRegistrationOptionsResponse> RegistrationOptionsAsync(
        Guid userId,
        PasskeyRegistrationOptionsRequest request,
        CancellationToken cancellationToken)
    {
        var user = await dbContext.Users.AsNoTracking().SingleOrDefaultAsync(
                       candidate => candidate.Id == userId,
                       cancellationToken)
                   ?? throw new ResourceNotFoundException("user");
        var challenge = await StoreChallengeAsync(
            userId,
            VerificationTokenType.PasskeyRegistrationChallenge,
            cancellationToken);
        var options = RegistrationOptions(user, challenge);

        return new PasskeyRegistrationOptionsResponse
        {
            Options = JsonSerializer.SerializeToElement(options),
            ExpiresAt = timeProvider.GetUtcNow() + _session.WebAuthnChallengeLifetime,
        };
    }

    public async Task<PasskeyResponse> CompleteRegistrationAsync(
        Guid userId,
        PasskeyRegistrationRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var raw = request.AttestationResponse.Deserialize<AuthenticatorAttestationRawResponse>()
                      ?? throw new InvalidTokenException();
            var challenge = ExtractChallenge(request.AttestationResponse);
            await ConsumeChallengeAsync(
                challenge,
                VerificationTokenType.PasskeyRegistrationChallenge,
                userId,
                cancellationToken);
            var user = await dbContext.Users.AsNoTracking().SingleAsync(
                candidate => candidate.Id == userId,
                cancellationToken);
            var result = await fido2.MakeNewCredentialAsync(
                new MakeNewCredentialParams
                {
                    AttestationResponse = raw,
                    OriginalOptions = RegistrationOptions(user, challenge),
                    IsCredentialIdUniqueToUserCallback = async (parameters, token) =>
                        !await dbContext.PasskeyCredentials.AnyAsync(
                            credential => credential.CredentialId == parameters.CredentialId,
                            token),
                },
                cancellationToken);
            var credential = new PasskeyCredential
            {
                UserId = userId,
                CredentialId = result.Id,
                PublicKey = result.PublicKey,
                SignCount = result.SignCount,
                Aaguid = result.AaGuid,
                Transports = result.Transports?.Select(value => value.ToString()).ToArray() ?? [],
                Label = request.Label?.Trim(),
            };
            dbContext.PasskeyCredentials.Add(credential);
            await dbContext.SaveChangesAsync(cancellationToken);
            await securityNotifications.NotifyAsync(
                userId,
                SecurityNotificationType.PasskeyAdded,
                cancellationToken);
            return ToResponse(credential);
        }
        catch (InvalidTokenException)
        {
            throw;
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            throw new InvalidTokenException();
        }
    }

    public async Task<PasskeyAuthenticationOptionsResponse> AuthenticationOptionsAsync(
        CancellationToken cancellationToken)
    {
        var challenge = await StoreChallengeAsync(
            null,
            VerificationTokenType.PasskeyAuthenticationChallenge,
            cancellationToken);
        var options = AssertionOptions(challenge);

        return new PasskeyAuthenticationOptionsResponse
        {
            Options = JsonSerializer.SerializeToElement(options),
            ExpiresAt = timeProvider.GetUtcNow() + _session.WebAuthnChallengeLifetime,
        };
    }

    public async Task<LoginResponse> CompleteAuthenticationAsync(
        PasskeyAuthenticationRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var raw = request.AssertionResponse.Deserialize<AuthenticatorAssertionRawResponse>()
                      ?? throw new InvalidCredentialsException();
            var challenge = ExtractChallenge(request.AssertionResponse);
            await ConsumeChallengeAsync(
                challenge,
                VerificationTokenType.PasskeyAuthenticationChallenge,
                null,
                cancellationToken);
            var credentialId = RawCredentialId(request.AssertionResponse);
            var credential = await dbContext.PasskeyCredentials.SingleOrDefaultAsync(
                                 candidate => candidate.CredentialId == credentialId,
                                 cancellationToken)
                             ?? throw new InvalidCredentialsException();
            var result = await fido2.MakeAssertionAsync(
                new MakeAssertionParams
                {
                    AssertionResponse = raw,
                    OriginalOptions = AssertionOptions(challenge),
                    StoredPublicKey = credential.PublicKey,
                    StoredSignatureCounter = checked((uint)credential.SignCount),
                    IsUserHandleOwnerOfCredentialIdCallback = (parameters, _) =>
                        Task.FromResult(
                            parameters.UserHandle.AsSpan().SequenceEqual(credential.UserId.ToByteArray())
                            && parameters.CredentialId.AsSpan().SequenceEqual(credential.CredentialId)),
                },
                cancellationToken);

            if (credential.SignCount != 0 && result.SignCount <= credential.SignCount)
            {
                throw new InvalidCredentialsException();
            }

            credential.SignCount = result.SignCount;
            credential.LastUsedAt = timeProvider.GetUtcNow();
            await dbContext.SaveChangesAsync(cancellationToken);
            var response = await sessionFactory.CreateAsync(
                credential.UserId,
                [AuthenticationMethod.Passkey],
                cancellationToken);
            await auditLogger.LogAsync(
                AuditEventType.LoginSucceeded,
                credential.UserId,
                new { Method = AuthenticationMethod.Passkey },
                cancellationToken);
            return response;
        }
        catch (InvalidCredentialsException)
        {
            throw;
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            throw new InvalidCredentialsException();
        }
    }

    public async Task<IReadOnlyList<PasskeyResponse>> ListAsync(
        Guid userId,
        CancellationToken cancellationToken) =>
        await dbContext.PasskeyCredentials
            .AsNoTracking()
            .Where(credential => credential.UserId == userId)
            .OrderByDescending(credential => credential.CreatedAt)
            .Select(credential => new PasskeyResponse
            {
                CredentialId = WebEncoders.Base64UrlEncode(credential.CredentialId),
                Label = credential.Label,
                Aaguid = credential.Aaguid == Guid.Empty ? null : credential.Aaguid,
                CreatedAt = credential.CreatedAt,
                LastUsedAt = credential.LastUsedAt,
            })
            .ToListAsync(cancellationToken);

    public async Task RemoveAsync(
        Guid userId,
        string credentialId,
        CancellationToken cancellationToken)
    {
        byte[] decoded;

        try
        {
            decoded = WebEncoders.Base64UrlDecode(credentialId);
        }
        catch (FormatException)
        {
            throw new ResourceNotFoundException("passkey");
        }

        var deleted = await dbContext.PasskeyCredentials
            .Where(credential => credential.UserId == userId
                                 && credential.CredentialId == decoded)
            .ExecuteDeleteAsync(cancellationToken);

        if (deleted == 0)
        {
            throw new ResourceNotFoundException("passkey");
        }

        await securityNotifications.NotifyAsync(
            userId,
            SecurityNotificationType.PasskeyRemoved,
            cancellationToken);
    }

    private CredentialCreateOptions RegistrationOptions(User user, string challenge)
    {
        var options = fido2.RequestNewCredential(
            new RequestNewCredentialParams
            {
                User = new Fido2User
                {
                    Id = user.Id.ToByteArray(),
                    Name = user.Email,
                    DisplayName = user.DisplayName ?? user.Email,
                },
                ExcludeCredentials = [],
                AuthenticatorSelection = new AuthenticatorSelection
                {
                    ResidentKey = ResidentKeyRequirement.Required,
                    UserVerification = UserVerificationRequirement.Required,
                },
                AttestationPreference = AttestationConveyancePreference.None,
            });
        options.Challenge = WebEncoders.Base64UrlDecode(challenge);
        return options;
    }

    private AssertionOptions AssertionOptions(string challenge)
    {
        var options = fido2.GetAssertionOptions(
            new GetAssertionOptionsParams
            {
                AllowedCredentials = [],
                UserVerification = UserVerificationRequirement.Required,
            });
        options.Challenge = WebEncoders.Base64UrlDecode(challenge);
        return options;
    }

    private async Task<string> StoreChallengeAsync(
        Guid? userId,
        VerificationTokenType type,
        CancellationToken cancellationToken)
    {
        var challenge = tokenGenerator.NewOpaqueToken();
        dbContext.VerificationTokens.Add(new VerificationToken
        {
            UserId = userId,
            Type = type,
            TokenHash = tokenGenerator.Hash(challenge),
            ExpiresAt = timeProvider.GetUtcNow() + _session.WebAuthnChallengeLifetime,
        });
        await dbContext.SaveChangesAsync(cancellationToken);
        return challenge;
    }

    private async Task ConsumeChallengeAsync(
        string challenge,
        VerificationTokenType type,
        Guid? userId,
        CancellationToken cancellationToken)
    {
        var hash = tokenGenerator.Hash(challenge);
        var now = timeProvider.GetUtcNow();
        var consumed = await dbContext.VerificationTokens
            .Where(token => token.TokenHash == hash
                            && token.Type == type
                            && token.UserId == userId
                            && token.ConsumedAt == null
                            && token.ExpiresAt > now)
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(token => token.ConsumedAt, now),
                cancellationToken);

        if (consumed == 0)
        {
            throw type == VerificationTokenType.PasskeyAuthenticationChallenge
                ? new InvalidCredentialsException()
                : new InvalidTokenException();
        }
    }

    private static string ExtractChallenge(JsonElement response)
    {
        var encoded = response.GetProperty("response").GetProperty("clientDataJSON").GetString()
                      ?? throw new InvalidTokenException();
        using var clientData = JsonDocument.Parse(WebEncoders.Base64UrlDecode(encoded));
        return clientData.RootElement.GetProperty("challenge").GetString()
               ?? throw new InvalidTokenException();
    }

    private static byte[] RawCredentialId(JsonElement response)
    {
        var encoded = response.GetProperty("rawId").GetString()
                      ?? response.GetProperty("id").GetString()
                      ?? throw new InvalidCredentialsException();
        return WebEncoders.Base64UrlDecode(encoded);
    }

    private static PasskeyResponse ToResponse(PasskeyCredential credential) =>
        new()
        {
            CredentialId = WebEncoders.Base64UrlEncode(credential.CredentialId),
            Label = credential.Label,
            Aaguid = credential.Aaguid == Guid.Empty ? null : credential.Aaguid,
            CreatedAt = credential.CreatedAt,
            LastUsedAt = credential.LastUsedAt,
        };
}
