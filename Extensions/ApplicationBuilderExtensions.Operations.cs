using Api.Services.Operations;
using Api.Services.Tokens;

namespace Api.Extensions;

public static partial class ApplicationBuilderExtensions
{
    private const string RotateSigningKey = "rotate-signing-key";
    private const string RetireSigningKeys = "retire-signing-keys";
    private const string MigrateDatabase = "migrate-database";

    /// <summary>
    /// Spelled once. The usage text, the dispatch labels and the unknown-command message all
    /// read from here, so a third command cannot be added to two of the three.
    /// </summary>
    private static readonly string Commands = string.Join(
        '|',
        [RotateSigningKey, RetireSigningKeys, MigrateDatabase]);

    /// <summary>
    /// Runs a bounded one-shot operational command instead of starting the HTTP server.
    /// </summary>
    public static async Task<bool> RunOperationalCommandAsync(
        this WebApplication app,
        string[] arguments)
    {
        if (arguments.Length == 0
            || !string.Equals(arguments[0], "operations", StringComparison.Ordinal))
        {
            return false;
        }

        if (arguments.Length != 2)
        {
            throw new InvalidOperationException($"Usage: operations <{Commands}>");
        }

        // Bounded one-shot work: the command runs to completion or the operator interrupts
        // the process. Stopping is the host's business and the host has not started.
        var cancellationToken = CancellationToken.None;

        await using var scope = app.Services.CreateAsyncScope();
        var logger = scope.ServiceProvider
            .GetRequiredService<ILoggerFactory>()
            .CreateLogger("OperationalCommand");

        switch (arguments[1])
        {
            case RotateSigningKey:
                var keyManager = scope.ServiceProvider.GetRequiredService<ISigningKeyManager>();
                var keyId = await keyManager.RotateAsync(cancellationToken);
                logger.LogInformation("Operational signing-key rotation completed. New kid {KeyId}.", keyId);
                break;

            case RetireSigningKeys:
                var retiringKeyManager = scope.ServiceProvider.GetRequiredService<ISigningKeyManager>();
                var count = await retiringKeyManager.RetireElapsedKeysAsync(cancellationToken);
                logger.LogInformation("Operational key retirement completed. Retired {Count} key(s).", count);
                break;

            case MigrateDatabase:
                await scope.ServiceProvider
                    .GetRequiredService<DatabaseDeploymentService>()
                    .DeployAsync(cancellationToken);
                break;

            default:
                throw new InvalidOperationException(
                    $"Unknown operational command '{arguments[1]}'. Expected one of: {Commands}.");
        }

        return true;
    }
}
