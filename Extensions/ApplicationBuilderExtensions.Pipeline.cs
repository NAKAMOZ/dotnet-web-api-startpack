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

        // TODO §12: add these two, in this order, once AddAuthenticationServices registers
        //           the JwtBearer, cookie and API-key schemes:
        //
        //               app.UseAuthentication();
        //               app.UseAuthorization();
        //
        // Order is not negotiable — authentication establishes who the caller is,
        // authorization decides what they may do with that identity. Reversed, every check
        // runs against an anonymous principal and the deny-by-default fallback rejects
        // everything.
        //
        // Neither can be added before §12, and the reason is worth recording because it is
        // not obvious: §5 configures a deny-by-default fallback policy, which the
        // authorization middleware applies even to requests that match no endpoint. With
        // no authentication scheme registered there is nothing to challenge with, so every
        // request — including a plain 404 — fails with an InvalidOperationException. The
        // policy itself is correct and is unit-tested in §5; only its pipeline activation
        // waits for a scheme to exist.

        app.MapControllers();

        return app;
    }
}
