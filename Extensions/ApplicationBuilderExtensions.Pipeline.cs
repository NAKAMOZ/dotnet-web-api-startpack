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
            //
            // AllowAnonymous is required as of §12: the deny-by-default fallback applies to
            // every endpoint without authorization metadata, and this one has none — without
            // the opt-out the document itself answers 401 in development.
            app.MapOpenApi().AllowAnonymous();
        }

        app.UseHttpsRedirection();

        // Order is not negotiable — authentication establishes who the caller is,
        // authorization decides what they may do with that identity. Reversed, every check
        // runs against an anonymous principal and denies everything.
        //
        // As of §12 these run against the real schemes: a policy scheme that forwards to
        // JwtBearer (bearer header or access cookie) or to ApiKey, and the deny-by-default
        // fallback is active behind them.
        app.UseAuthentication();
        app.UseAuthorization();

        app.MapControllers();

        return app;
    }
}
