using Api.DTOs.Auth;
using Api.Extensions;
using Api.Validators.Common;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;

namespace UnitTests.Validators;

/// <summary>
/// Boundary tests for the shared password policy, exercised through the registration
/// validator — the policy is only worth testing as the endpoints actually apply it.
/// </summary>
public class PasswordRulesTests
{
    private readonly IValidator<RegisterRequest> _validator;

    public PasswordRulesTests()
    {
        var services = new ServiceCollection().AddValidationServices().BuildServiceProvider();
        _validator = services.GetRequiredService<IValidator<RegisterRequest>>();
    }

    private IEnumerable<string> ErrorCodesFor(string password, string email = "someone@example.com") =>
        _validator
            .Validate(new RegisterRequest { Email = email, Password = password })
            .Errors
            .Select(failure => failure.ErrorCode);

    [Theory]
    [InlineData("correct horse battery staple")]  // a passphrase: long, spaces, no symbols
    [InlineData("Tr0ub4dor&3xtra-long")]
    [InlineData("žluťoučký-kůň-úpěl")]            // non-ASCII must be accepted, not stripped
    public void AcceptsLongUnpredictablePasswords(string password) =>
        Assert.Empty(ErrorCodesFor(password));

    [Theory]
    [InlineData("short")]
    [InlineData("elevenchars")]
    public void RejectsPasswordsBelowTheMinimum(string password) =>
        Assert.Contains(ValidationErrorCodes.PasswordTooShort, ErrorCodesFor(password));

    [Fact]
    public void RejectsPasswordsAboveTheMaximum()
    {
        // The cap is a cost control, not a security one: Argon2id hashes whatever it is
        // given, so an unbounded password is unbounded deliberate work on an anonymous
        // endpoint.
        var password = new string('x', PasswordRules.MaximumLength + 1);

        Assert.Contains(ValidationErrorCodes.PasswordTooLong, ErrorCodesFor(password));
    }

    [Theory]
    [InlineData("passwordpassword")]
    [InlineData("PASSWORDPASSWORD")]   // the deny list is case-insensitive
    [InlineData("Summer2024!!!")]
    public void RejectsDenyListedPasswords(string password) =>
        Assert.Contains(ValidationErrorCodes.PasswordTooCommon, ErrorCodesFor(password));

    [Theory]
    [InlineData("aaaaaaaaaaaaaa")]         // one repeated character
    [InlineData("abcdefghijklmn")]         // alphabet run
    [InlineData("nmlkjihgfedcba")]         // and backwards
    [InlineData("qwertyuiopasdfghjkl")]    // keyboard rows
    public void RejectsPredictablePatterns(string password) =>
        Assert.Contains(ValidationErrorCodes.PasswordPredictablePattern, ErrorCodesFor(password));

    [Fact]
    public void RejectsPasswordsContainingTheOwnEmailLocalPart() =>
        Assert.Contains(
            ValidationErrorCodes.PasswordContainsEmail,
            ErrorCodesFor("nevzatcelikkanat-2026", "nevzatcelikkanat@example.com"));

    [Fact]
    public void AllowsVeryShortLocalPartsToAppearInPasswords() =>
        // "me" is not a predictable password component. Rejecting every password containing
        // a two-letter local part would reject far more than it protects.
        Assert.DoesNotContain(
            ValidationErrorCodes.PasswordContainsEmail,
            ErrorCodesFor("something memorable here", "me@example.com"));

    [Fact]
    public void TheDenyListActuallyLoaded() =>
        // Guards the embedded resource: if the file stopped being embedded, every deny-list
        // test above would pass vacuously against an empty set.
        Assert.True(PasswordDenyList.Count > 100);
}
