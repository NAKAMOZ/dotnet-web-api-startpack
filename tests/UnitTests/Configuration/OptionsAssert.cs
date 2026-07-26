using Microsoft.Extensions.Options;

namespace UnitTests.Configuration;

/// <summary>
/// Assertions over <see cref="ValidateOptionsResult"/>.
/// </summary>
/// <remarks>
/// Deliberately not named <c>Assert</c>: a type of that name in this namespace hides
/// <see cref="Xunit.Assert"/> for every file in it, so each xUnit assertion a future test
/// wants has to be re-exported here first.
/// </remarks>
internal static class OptionsAssert
{
    public static void Failed(ValidateOptionsResult result) =>
        Assert.True(result.Failed, "Expected options validation to fail.");

    public static void Succeeded(ValidateOptionsResult result) =>
        Assert.True(result.Succeeded, result.FailureMessage);
}
