using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Api.Extensions;

public static partial class ApplicationBuilderExtensions
{
    /// <summary>Maps anonymous probes with status-only responses.</summary>
    public static WebApplication MapApplicationHealthChecks(this WebApplication app)
    {
        app.MapHealthChecks(
                "/health/live",
                CreateOptions("live"))
            .AllowAnonymous();

        app.MapHealthChecks(
                "/health/ready",
                CreateOptions("ready"))
            .AllowAnonymous();

        return app;
    }

    private static HealthCheckOptions CreateOptions(string tag) =>
        new()
        {
            Predicate = registration => registration.Tags.Contains(tag),
            ResponseWriter = WriteMinimalResponseAsync,
        };

    private static Task WriteMinimalResponseAsync(HttpContext context, HealthReport report)
    {
        context.Response.ContentType = "text/plain; charset=utf-8";
        return context.Response.WriteAsync(
            report.Status.ToString(),
            context.RequestAborted);
    }
}
