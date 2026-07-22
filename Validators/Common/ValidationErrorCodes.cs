namespace Api.Validators.Common;

/// <summary>
/// Stable machine-readable codes attached to every validation rule, surfaced in the
/// Problem Details <c>errorCodes</c> extension alongside the human-readable messages.
/// </summary>
/// <remarks>
/// The messages are English and may be reworded at any time; these codes are the contract.
/// A client localising "your password is too short" must branch on
/// <see cref="PasswordTooShort"/>, not on the sentence — otherwise every copy edit is a
/// breaking change for every translated client.
/// <para>
/// Codes are <c>snake_case</c> and never reused for a different meaning. Retiring a rule
/// retires its code with it.
/// </para>
/// </remarks>
public static class ValidationErrorCodes
{
    public const string Required = "required";
    public const string TooLong = "too_long";
    public const string OutOfRange = "out_of_range";

    public const string EmailInvalid = "email_invalid";
    public const string EmailTooLong = "email_too_long";

    public const string PasswordTooShort = "password_too_short";
    public const string PasswordTooLong = "password_too_long";

    /// <summary>The password appears in the deny list of predictable passwords.</summary>
    public const string PasswordTooCommon = "password_too_common";

    /// <summary>The password is one character repeated, or a run along a keyboard or the alphabet.</summary>
    public const string PasswordPredictablePattern = "password_predictable_pattern";

    /// <summary>The password contains the local part of the account's own email address.</summary>
    public const string PasswordContainsEmail = "password_contains_email";

    /// <summary>The new password is the same as the current one.</summary>
    public const string PasswordUnchanged = "password_unchanged";

    public const string PageOutOfRange = "page_out_of_range";
    public const string PageSizeOutOfRange = "page_size_out_of_range";

    /// <summary>The requested sort field is not on the allow-list for this endpoint.</summary>
    public const string SortFieldNotAllowed = "sort_field_not_allowed";

    /// <summary>A requested API-key scope is not a known permission constant.</summary>
    public const string ScopeUnknown = "scope_unknown";

    public const string ScopesEmpty = "scopes_empty";

    /// <summary>A TOTP or recovery code that is not shaped like either.</summary>
    public const string CodeMalformed = "code_malformed";

    /// <summary>An expiry that is already in the past.</summary>
    public const string ExpiryInPast = "expiry_in_past";

    /// <summary>The OAuth callback arrived without the state parameter that binds it to an authorize request.</summary>
    public const string StateMissing = "state_missing";

    /// <summary>The callback carried neither an authorization code nor an error.</summary>
    public const string CallbackIncomplete = "callback_incomplete";

    /// <summary>A date range whose end precedes its start.</summary>
    public const string RangeInverted = "range_inverted";
}
