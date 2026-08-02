namespace Api.Services.Email;

/// <summary>Stable, non-secret labels for credential-change notifications.</summary>
public static class SecurityNotificationType
{
    public const string PasswordChanged = "Password changed";
    public const string PasswordReset = "Password reset completed";
    public const string MfaEnabled = "Multi-factor authentication enabled";
    public const string MfaDisabled = "Multi-factor authentication disabled";
    public const string RecoveryCodesRegenerated = "Recovery codes regenerated";
    public const string PasskeyAdded = "Passkey added";
    public const string PasskeyRemoved = "Passkey removed";
    public const string ApiKeyCreated = "API key created";
    public const string ApiKeyRevoked = "API key revoked";
    public const string LinkedAccountRemoved = "Linked sign-in account removed";
}
