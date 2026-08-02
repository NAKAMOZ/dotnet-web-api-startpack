using System.Text.RegularExpressions;
using Api.Data;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Api.Services.Operations;

/// <summary>
/// Applies EF migrations with the deployment connection and provisions the least-privilege
/// runtime login used by the continuously running API.
/// </summary>
public sealed class DatabaseDeploymentService(
    AppDbContext database,
    IConfiguration configuration,
    ILogger<DatabaseDeploymentService> logger)
{
    private static readonly Regex RoleNamePattern = new(
        "^[a-z][a-z0-9_]{0,62}$",
        RegexOptions.CultureInvariant | RegexOptions.NonBacktracking);

    /// <summary>Runs the idempotent migration and grants sequence.</summary>
    public async Task DeployAsync(CancellationToken cancellationToken)
    {
        var runtimeRole = configuration["DatabaseDeployment:RuntimeRole"];
        var runtimePassword = configuration["DatabaseDeployment:RuntimePassword"];
        if (string.IsNullOrWhiteSpace(runtimeRole)
            || !RoleNamePattern.IsMatch(runtimeRole)
            || string.IsNullOrWhiteSpace(runtimePassword)
            || runtimePassword.Length < 20)
        {
            throw new InvalidOperationException(
                "Database deployment requires DatabaseDeployment:RuntimeRole (lowercase identifier) " +
                "and DatabaseDeployment:RuntimePassword (at least 20 characters).");
        }

        logger.LogInformation("Applying pending EF Core database migrations.");
        await database.Database.MigrateAsync(cancellationToken);

        var connection = (NpgsqlConnection)database.Database.GetDbConnection();
        await database.Database.OpenConnectionAsync(cancellationToken);
        try
        {
            await EnsureRuntimeRoleAsync(
                connection,
                runtimeRole,
                runtimePassword,
                cancellationToken);
            await GrantRuntimePrivilegesAsync(connection, runtimeRole, cancellationToken);
        }
        finally
        {
            await database.Database.CloseConnectionAsync();
        }

        logger.LogInformation(
            "Database migrations and least-privilege grants completed for runtime role {RuntimeRole}.",
            runtimeRole);
    }

    private static async Task EnsureRuntimeRoleAsync(
        NpgsqlConnection connection,
        string runtimeRole,
        string runtimePassword,
        CancellationToken cancellationToken)
    {
        // PostgreSQL doesn't accept a bind parameter in CREATE/ALTER ROLE ... PASSWORD.
        // Put both values in transaction-local settings, then quote them inside PostgreSQL
        // with format(%I/%L). The password never becomes command text, logs or telemetry.
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        await using (var settings = connection.CreateCommand())
        {
            settings.Transaction = transaction;
            settings.CommandText = """
                SELECT set_config('startpack.runtime_role', @role, true),
                       set_config('startpack.runtime_password', @password, true);
                """;
            settings.Parameters.AddWithValue("role", runtimeRole);
            settings.Parameters.AddWithValue("password", runtimePassword);
            await settings.ExecuteNonQueryAsync(cancellationToken);
        }

        await using (var role = connection.CreateCommand())
        {
            role.Transaction = transaction;
            role.CommandText = """
                DO $deployment$
                BEGIN
                    IF EXISTS (
                        SELECT 1 FROM pg_roles
                        WHERE rolname = current_setting('startpack.runtime_role')) THEN
                        EXECUTE format(
                            'ALTER ROLE %I WITH LOGIN PASSWORD %L',
                            current_setting('startpack.runtime_role'),
                            current_setting('startpack.runtime_password'));
                    ELSE
                        EXECUTE format(
                            'CREATE ROLE %I WITH LOGIN PASSWORD %L',
                            current_setting('startpack.runtime_role'),
                            current_setting('startpack.runtime_password'));
                    END IF;
                END
                $deployment$;
                """;
            await role.ExecuteNonQueryAsync(cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
    }

    private async Task GrantRuntimePrivilegesAsync(
        NpgsqlConnection connection,
        string runtimeRole,
        CancellationToken cancellationToken)
    {
        var quotedRole = QuoteIdentifier(runtimeRole);
        var quotedDatabase = QuoteIdentifier(connection.Database);
        await using var command = connection.CreateCommand();
        command.CommandText = $"""
            GRANT CONNECT ON DATABASE {quotedDatabase} TO {quotedRole};
            GRANT USAGE ON SCHEMA {QuoteIdentifier(AppDbContext.Schema)} TO {quotedRole};
            GRANT SELECT, INSERT, UPDATE, DELETE ON ALL TABLES IN SCHEMA {QuoteIdentifier(AppDbContext.Schema)} TO {quotedRole};
            GRANT USAGE, SELECT, UPDATE ON ALL SEQUENCES IN SCHEMA {QuoteIdentifier(AppDbContext.Schema)} TO {quotedRole};
            ALTER DEFAULT PRIVILEGES IN SCHEMA {QuoteIdentifier(AppDbContext.Schema)}
                GRANT SELECT, INSERT, UPDATE, DELETE ON TABLES TO {quotedRole};
            ALTER DEFAULT PRIVILEGES IN SCHEMA {QuoteIdentifier(AppDbContext.Schema)}
                GRANT USAGE, SELECT, UPDATE ON SEQUENCES TO {quotedRole};
            """;
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static string QuoteIdentifier(string value) =>
        $"\"{value.Replace("\"", "\"\"", StringComparison.Ordinal)}\"";
}
