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

        // Order is not negotiable — authentication establishes who the caller is,
        // authorization decides what they may do with that identity. Reversed, every check
        // runs against an anonymous principal and denies everything.
        //
        // Both are active as of §11, against the placeholder scheme registered in
        // AddAuthenticationServices. That scheme authenticates nobody, so protected
        // endpoints answer 401 — the correct response for an unauthenticated caller, and
        // the reason these two lines could be added before the real schemes exist. Without
        // a challenge scheme an [Authorize] endpoint throws instead, and every protected
        // route in the inventory would answer 500.
        //
        // Still outstanding for §12: replacing the placeholder with JwtBearer, cookie and
        // API-key schemes, and only then setting AuthorizationOptions.FallbackPolicy to
        // DenyByDefault (§5's last open item). The fallback stays off until then because it
        // applies to requests matching no endpoint as well, which would turn every 404 into
        // a 401.
        app.UseAuthentication();
        app.UseAuthorization();

        app.MapControllers();

        return app;
    }
}
