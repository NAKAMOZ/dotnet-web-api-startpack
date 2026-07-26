using Api.Services.Tokens;

namespace Api.Extensions;

public static partial class ApplicationBuilderExtensions
{
    /// <summary>
    /// Runs a bounded one-shot operational command instead of starting the HTTP server.
    /// </summary>
    public static async Task<bool> RunOperationalCommandAsync(
        this WebApplication app,
        string[] arguments,
        CancellationToken cancellationToken = default)
    {
        if (arguments.Length == 0
            || !string.Equals(arguments[0], "operations", StringComparison.Ordinal))
        {
            return false;
        }

        if (arguments.Length != 2)
        {
            throw new InvalidOperationException(
                "Usage: operations <rotate-signing-key|retire-signing-keys>");
        }

        await using var scope = app.Services.CreateAsyncScope();
        var keyManager = scope.ServiceProvider.GetRequiredService<ISigningKeyManager>();
        var logger = scope.ServiceProvider
            .GetRequiredService<ILoggerFactory>()
            .CreateLogger("OperationalCommand");

        switch (arguments[1])
        {
            case "rotate-signing-key":
                var keyId = await keyManager.RotateAsync(cancellationToken);
                logger.LogInformation("Operational signing-key rotation completed. New kid {KeyId}.", keyId);
                break;

            case "retire-signing-keys":
                var count = await keyManager.RetireElapsedKeysAsync(cancellationToken);
                logger.LogInformation("Operational key retirement completed. Retired {Count} key(s).", count);
                break;

            default:
                throw new InvalidOperationException(
                    $"Unknown operational command '{arguments[1]}'. " +
                    "Expected rotate-signing-key or retire-signing-keys.");
        }

        return true;
    }
}
