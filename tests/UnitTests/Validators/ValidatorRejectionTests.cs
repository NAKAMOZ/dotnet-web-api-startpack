using Api.DTOs.Admin;
using Api.DTOs.ApiKeys;
using Api.DTOs.Auth;
using Api.DTOs.EmailVerification;
using Api.DTOs.Mfa;
using Api.DTOs.Passkeys;
using Api.DTOs.PasswordReset;
using Api.DTOs.SocialAuth;
using Api.DTOs.Users;
using Api.Extensions;
using Api.Models.Enums;
using Api.Validators.Common;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;

namespace UnitTests.Validators;

/// <summary>One rejecting boundary for every validator rule owned by a request contract.</summary>
public sealed class ValidatorRejectionTests
{
    private const string ValidPassword = "V4lid!River-Stone-Cobalt-47";

    private readonly ServiceProvider _services =
        new ServiceCollection().AddValidationServices().BuildServiceProvider();

    public static TheoryData<object, string> InvalidRequests => new()
    {
        { new ConfirmEmailRequest { Token = "" }, ValidationErrorCodes.Required },
        { new ConfirmEmailRequest { Token = Text(257) }, ValidationErrorCodes.TooLong },
        { new SocialCallbackQuery { Code = "code" }, ValidationErrorCodes.StateMissing },
        { new SocialCallbackQuery { State = Text(1025), Code = "code" }, ValidationErrorCodes.TooLong },
        { new SocialCallbackQuery { State = "state" }, ValidationErrorCodes.CallbackIncomplete },
        { new SocialCallbackQuery { State = "state", Code = Text(2049) }, ValidationErrorCodes.TooLong },
        { new SocialCallbackQuery { State = "state", Error = Text(257) }, ValidationErrorCodes.TooLong },
        { new PasswordResetRequest { Email = "" }, ValidationErrorCodes.Required },
        { new PasswordResetRequest { Email = "not-an-email" }, ValidationErrorCodes.EmailInvalid },
        { new PasswordResetRequest { Email = $"{Text(250)}@x.io" }, ValidationErrorCodes.EmailTooLong },
        { new PasswordResetRequest { Email = " user@example.com" }, ValidationErrorCodes.EmailInvalid },
        { new PasswordResetConfirmRequest { Token = "", NewPassword = ValidPassword }, ValidationErrorCodes.Required },
        { new PasswordResetConfirmRequest { Token = Text(257), NewPassword = ValidPassword }, ValidationErrorCodes.TooLong },
        { new PasswordResetConfirmRequest { Token = "token", NewPassword = "short" }, ValidationErrorCodes.PasswordTooShort },
        { new RefreshRequest { RefreshToken = Text(513) }, ValidationErrorCodes.TooLong },
        { new LoginRequest { Email = "", Password = "password" }, ValidationErrorCodes.Required },
        { new LoginRequest { Email = Text(255), Password = "password" }, ValidationErrorCodes.EmailTooLong },
        { new LoginRequest { Email = "user@example.com", Password = "" }, ValidationErrorCodes.Required },
        { new LoginRequest { Email = "user@example.com", Password = Text(257) }, ValidationErrorCodes.PasswordTooLong },
        { new MfaLoginRequest { MfaTicket = "", Code = "123456" }, ValidationErrorCodes.Required },
        { new MfaLoginRequest { MfaTicket = Text(257), Code = "123456" }, ValidationErrorCodes.TooLong },
        { new MfaLoginRequest { MfaTicket = "ticket", Code = "" }, ValidationErrorCodes.Required },
        { new MfaLoginRequest { MfaTicket = "ticket", Code = "12345" }, ValidationErrorCodes.CodeMalformed },
        { new RegisterRequest { Email = "bad", Password = ValidPassword }, ValidationErrorCodes.EmailInvalid },
        { new RegisterRequest { Email = "user@example.com", Password = ValidPassword, DisplayName = Text(101) }, ValidationErrorCodes.TooLong },
        { new UpdateProfileRequest { DisplayName = Text(101) }, ValidationErrorCodes.TooLong },
        { new ChangePasswordRequest { CurrentPassword = "", NewPassword = ValidPassword }, ValidationErrorCodes.Required },
        { new ChangePasswordRequest { CurrentPassword = Text(257), NewPassword = ValidPassword }, ValidationErrorCodes.PasswordTooLong },
        { new ChangePasswordRequest { CurrentPassword = ValidPassword, NewPassword = "short" }, ValidationErrorCodes.PasswordTooShort },
        { new ChangePasswordRequest { CurrentPassword = ValidPassword, NewPassword = ValidPassword }, ValidationErrorCodes.PasswordUnchanged },
        { new ConfirmTotpRequest { Code = "" }, ValidationErrorCodes.Required },
        { new ConfirmTotpRequest { Code = "12ab56" }, ValidationErrorCodes.CodeMalformed },
        { new PasskeyRegistrationOptionsRequest { Label = Text(101) }, ValidationErrorCodes.TooLong },
        { new PasskeyRegistrationRequest { AttestationResponse = default }, ValidationErrorCodes.Required },
        { new PasskeyRegistrationRequest { AttestationResponse = JsonObject(), Label = Text(101) }, ValidationErrorCodes.TooLong },
        { new PasskeyAuthenticationRequest { AssertionResponse = default }, ValidationErrorCodes.Required },
        { new CreateApiKeyRequest { Name = "", Scopes = ["users.read.any"] }, ValidationErrorCodes.Required },
        { new CreateApiKeyRequest { Name = Text(101), Scopes = ["users.read.any"] }, ValidationErrorCodes.TooLong },
        { new CreateApiKeyRequest { Name = "key", Scopes = [] }, ValidationErrorCodes.ScopesEmpty },
        { new CreateApiKeyRequest { Name = "key", Scopes = ["invented.scope"] }, ValidationErrorCodes.ScopeUnknown },
        { new CreateApiKeyRequest { Name = "key", Scopes = ["users.read.any"], ExpiresAt = DateTimeOffset.UnixEpoch }, ValidationErrorCodes.ExpiryInPast },
        { new AssignRoleRequest { RoleId = Guid.Empty }, ValidationErrorCodes.Required },
        { new AdminUpdateUserRequest(), ValidationErrorCodes.Required },
        { new AdminUpdateUserRequest { DisplayName = Text(101) }, ValidationErrorCodes.TooLong },
        { new AdminUserListQuery { Page = 0 }, ValidationErrorCodes.PageOutOfRange },
        { new AdminUserListQuery { PageSize = 101 }, ValidationErrorCodes.PageSizeOutOfRange },
        { new AdminUserListQuery { Sort = "passwordHash" }, ValidationErrorCodes.SortFieldNotAllowed },
        { new AdminUserListQuery { Search = Text(257) }, ValidationErrorCodes.TooLong },
        { new AdminUserListQuery { Role = Text(65) }, ValidationErrorCodes.TooLong },
        { new AuditLogQuery { CorrelationId = Text(65) }, ValidationErrorCodes.TooLong },
        { new AuditLogQuery { From = DateTimeOffset.UtcNow, To = DateTimeOffset.UnixEpoch }, ValidationErrorCodes.RangeInverted },
        { new AuditLogQuery { EventType = (AuditEventType)999 }, ValidationErrorCodes.OutOfRange },
    };

    [Theory]
    [MemberData(nameof(InvalidRequests))]
    public void RuleBoundary_IsRejectedWithStableCode(object request, string expectedCode)
    {
        var validatorType = typeof(IValidator<>).MakeGenericType(request.GetType());
        var validator = (IValidator)_services.GetRequiredService(validatorType);
        var result = validator.Validate(new ValidationContext<object>(request));

        Assert.Contains(expectedCode, result.Errors.Select(error => error.ErrorCode));
    }

    private static string Text(int length) => new('x', length);

    private static System.Text.Json.JsonElement JsonObject() =>
        System.Text.Json.JsonDocument.Parse("{}").RootElement.Clone();
}
