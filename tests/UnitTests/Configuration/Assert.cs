using Microsoft.Extensions.Options;

namespace UnitTests.Configuration;

internal static class Assert
{
    public static void Failed(ValidateOptionsResult result) =>
        Xunit.Assert.True(result.Failed, "Expected options validation to fail.");

    public static void Succeeded(ValidateOptionsResult result) =>
        Xunit.Assert.True(result.Succeeded, result.FailureMessage);

    public static void Contains(string expectedSubstring, string? actualString) =>
        Xunit.Assert.Contains(expectedSubstring, actualString ?? string.Empty, StringComparison.Ordinal);
}
