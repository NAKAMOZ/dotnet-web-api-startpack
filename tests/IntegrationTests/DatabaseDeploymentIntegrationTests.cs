using Api.Data;
using Api.Services.Operations;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;

namespace IntegrationTests;

[Collection(IntegrationTestCollection.Name)]
public sealed class DatabaseDeploymentIntegrationTests(IntegrationTestFactory factory)
{
    [Fact]
    public async Task Deployment_IsIdempotentAndRuntimeRoleCanUseButNotAlterSchema()
    {
        await factory.ResetAsync();
        const string role = "integration_runtime";
        const string password = "Integration-Runtime-Password-47!";
        var cancellationToken = TestContext.Current.CancellationToken;
        var runtimeConnectionString = await factory.InScopeAsync(async services =>
        {
            var database = services.GetRequiredService<AppDbContext>();
            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["DatabaseDeployment:RuntimeRole"] = role,
                    ["DatabaseDeployment:RuntimePassword"] = password,
                })
                .Build();
            var deployment = new DatabaseDeploymentService(
                database,
                configuration,
                NullLogger<DatabaseDeploymentService>.Instance);

            await deployment.DeployAsync(cancellationToken);
            await deployment.DeployAsync(cancellationToken);

            var builder = new NpgsqlConnectionStringBuilder(database.Database.GetConnectionString())
            {
                Username = role,
                Password = password,
            };
            return builder.ConnectionString;
        });

        await using var runtimeConnection = new NpgsqlConnection(runtimeConnectionString);
        await runtimeConnection.OpenAsync(cancellationToken);
        await using (var read = runtimeConnection.CreateCommand())
        {
            read.CommandText = "SELECT COUNT(*) FROM auth.\"Roles\";";
            Assert.Equal(2L, await read.ExecuteScalarAsync(cancellationToken));
        }

        await using (var write = runtimeConnection.CreateCommand())
        {
            write.CommandText = "UPDATE auth.\"Users\" SET \"UpdatedAt\" = \"UpdatedAt\" WHERE FALSE;";
            Assert.Equal(0, await write.ExecuteNonQueryAsync(cancellationToken));
        }

        await using var ddl = runtimeConnection.CreateCommand();
        ddl.CommandText = "CREATE TABLE auth.\"ForbiddenRuntimeDdl\" (\"Id\" integer);";
        var exception = await Assert.ThrowsAsync<PostgresException>(
            () => ddl.ExecuteNonQueryAsync(cancellationToken));
        Assert.Equal(PostgresErrorCodes.InsufficientPrivilege, exception.SqlState);
    }
}
