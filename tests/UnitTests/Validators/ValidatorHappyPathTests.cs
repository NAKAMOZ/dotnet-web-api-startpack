using System.Text.Json;
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
using Api.Handlers.Authorization;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;

namespace UnitTests.Validators;

/// <summary>
/// One accepted boundary fixture per validator. Rule-specific rejection tests live beside
/// shared rules and decision-heavy query validators; this guard stops a new rule from making
/// the documented normal request impossible.
/// </summary>
public class ValidatorHappyPathTests
{
    private readonly ServiceProvider _services =
        new ServiceCollection().AddValidationServices().BuildServiceProvider();

    public static TheoryData<object> ValidRequests
    {
        get
        {
            var jsonObject = JsonDocument.Parse("{}").RootElement.Clone();
            const string strongPassword = "V4lid!River-Stone-Cobalt-47";

            return
            [
                new ConfirmEmailRequest { Token = "verification-token" },
                new SocialCallbackQuery { Code = "provider-code", State = "signed-state" },
                new PasswordResetRequest { Email = "user@example.com" },
                new PasswordResetConfirmRequest
                {
                    Token = "reset-token",
                    NewPassword = strongPassword,
                },
                new RefreshRequest(),
                new LoginRequest { Email = "user@example.com", Password = "existing password" },
                new MfaLoginRequest { MfaTicket = "ticket", Code = "123456" },
                new RegisterRequest
                {
                    Email = "user@example.com",
                    Password = strongPassword,
                    DisplayName = "User",
                },
                new UpdateProfileRequest { DisplayName = "User" },
                new ChangePasswordRequest
                {
                    CurrentPassword = "existing password",
                    NewPassword = strongPassword,
                },
                new ConfirmTotpRequest { Code = "123456" },
                new PasskeyRegistrationOptionsRequest { Label = "Laptop" },
                new PasskeyRegistrationRequest
                {
                    AttestationResponse = jsonObject,
                    Label = "Laptop",
                },
                new PasskeyAuthenticationOptionsRequest(),
                new PasskeyAuthenticationRequest { AssertionResponse = jsonObject },
                new CreateApiKeyRequest
                {
                    Name = "CI",
                    Scopes = [Permissions.UsersReadAny],
                    ExpiresAt = DateTimeOffset.UtcNow.AddYears(1),
                },
                new AssignRoleRequest
                {
                    RoleId = Guid.Parse("01900000-0000-7000-8000-000000000030"),
                },
                new AdminUpdateUserRequest { Unlock = true },
                new AdminUserListQuery { Page = 1, PageSize = 100, Sort = "createdAt:desc" },
                new AuditLogQuery
                {
                    Page = 1,
                    PageSize = 100,
                    From = new DateTimeOffset(2026, 7, 1, 0, 0, 0, TimeSpan.Zero),
                    To = new DateTimeOffset(2026, 7, 31, 0, 0, 0, TimeSpan.Zero),
                },
            ];
        }
    }

    [Theory]
    [MemberData(nameof(ValidRequests))]
    public void DocumentedHappyPath_IsAccepted(object request)
    {
        var validatorType = typeof(IValidator<>).MakeGenericType(request.GetType());
        var validator = (IValidator)_services.GetRequiredService(validatorType);

        var result = validator.Validate(new ValidationContext<object>(request));

        Assert.True(
            result.IsValid,
            $"{request.GetType().Name}: {string.Join("; ", result.Errors.Select(error => error.ErrorMessage))}");
    }
}
