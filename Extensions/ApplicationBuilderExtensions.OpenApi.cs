using Scalar.AspNetCore;

namespace Api.Extensions;

public static partial class ApplicationBuilderExtensions
{
    /// <summary>
    /// Maps machine-readable OpenAPI and Scalar in development and staging, never production.
    /// </summary>
    public static WebApplication MapApiDocumentation(this WebApplication app)
    {
        if (!app.Environment.IsDevelopment() && !app.Environment.IsStaging())
        {
            return app;
        }

        app.MapOpenApi("/openapi/{documentName}.json")
            .AllowAnonymous();

        app.MapScalarApiReference(options =>
            options
                .WithTitle("dotnet-web-api-startpack")
                .WithOpenApiRoutePattern("/openapi/{documentName}.json")
                .DisableDefaultFonts()
                .AddPreferredSecuritySchemes(
                [
                    Configuration.OpenApi.SecuritySchemeTransformer.BearerScheme,
                    Configuration.OpenApi.SecuritySchemeTransformer.CookieScheme,
                    Configuration.OpenApi.SecuritySchemeTransformer.ApiKeyScheme,
                ]))
            .AllowAnonymous();

        return app;
    }
}
