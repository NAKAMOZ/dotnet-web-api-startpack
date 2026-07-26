using Api.Configuration;
using Microsoft.Extensions.Options;

namespace Api.Extensions;

public static partial class ApplicationBuilderExtensions
{
    /// <summary>
    /// Applies forwarded headers before any component reads the scheme or remote address.
    /// </summary>
    public static WebApplication UseTrustedForwardedHeaders(this WebApplication app)
    {
        if (app.Services.GetRequiredService<IOptions<ReverseProxyOptions>>().Value.Enabled)
        {
            app.UseForwardedHeaders();
        }

        return app;
    }
}
