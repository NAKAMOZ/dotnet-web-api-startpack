using Api.Configuration;
using Microsoft.Extensions.Options;

namespace Api.Extensions;

public static partial class ServiceCollectionExtensions
{
    /// <summary>Registers the explicit reverse-proxy trust boundary (§27).</summary>
    public static IServiceCollection AddForwardedHeaderServices(this IServiceCollection services)
    {
        services.AddSingleton<IConfigureOptions<ForwardedHeadersOptions>, ConfigureForwardedHeadersOptions>();
        return services;
    }
}
