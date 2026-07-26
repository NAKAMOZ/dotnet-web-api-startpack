using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;

namespace IntegrationTests;

public sealed class ConfigurationStartupTests
{
    [Fact]
    public void ProductionWithoutTrustedProxy_HostFailsBeforeServingTraffic()
    {
        using var factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                // Deliberately not UseProductionLikeHost: the missing proxy configuration is
                // what this test asserts on.
                builder.UseEnvironment("Production");
                builder.UseSetting(
                    "ConnectionStrings:Postgres",
                    "Host=localhost;Database=configuration-startup-tests");
            });

        var exception = Assert.ThrowsAny<Exception>(() => factory.CreateClient());
        var validation = FindOptionsValidationException(exception);

        Assert.NotNull(validation);
        Assert.Contains("ReverseProxy:Enabled", validation.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void MissingJwtIssuer_HostFailsBeforeServingTraffic()
    {
        using var factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.UseProductionLikeHost("Production", "configuration-startup-tests");
                builder.ConfigureAppConfiguration(configuration =>
                    configuration.AddInMemoryCollection(new Dictionary<string, string?>
                    {
                        ["Jwt:Issuer"] = string.Empty,
                    }));
            });

        var exception = Assert.ThrowsAny<Exception>(() => factory.CreateClient());
        var validation = FindOptionsValidationException(exception);

        Assert.NotNull(validation);
        Assert.Contains("Issuer", validation.Message, StringComparison.Ordinal);
    }

    private static OptionsValidationException? FindOptionsValidationException(Exception exception)
    {
        for (Exception? current = exception; current is not null; current = current.InnerException)
        {
            if (current is OptionsValidationException validation)
            {
                return validation;
            }
        }

        return null;
    }
}
