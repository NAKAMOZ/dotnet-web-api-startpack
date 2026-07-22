namespace Api.Extensions;

/// <summary>
/// Request-pipeline composition. Middleware order is security-relevant and is asserted
/// by tests in §22 — do not reorder without reading that suite first.
/// </summary>
public static partial class ApplicationBuilderExtensions
{
    /// <summary>
    /// Builds the HTTP request pipeline and maps the controller endpoints.
    /// </summary>
    public static WebApplication UseApiPipeline(this WebApplication app)
    {
        // TODO §14: correlation ID, security headers, exception handling → Problem Details.
        // TODO §17: rate limiting.

        if (app.Environment.IsDevelopment())
        {
            // OpenAPI document is exposed in Development only; Scalar UI arrives in §18
            // and is disabled in production (P16, still pending).
            app.MapOpenApi();
        }

        app.UseHttpsRedirection();

        // TODO §4: app.UseAuthentication() / app.UseAuthorization() once schemes exist.

        app.MapControllers();

        return app;
    }
}
