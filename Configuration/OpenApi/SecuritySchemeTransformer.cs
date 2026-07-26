using Microsoft.AspNetCore.OpenApi;
using Microsoft.Extensions.Options;
using Microsoft.OpenApi;

namespace Api.Configuration.OpenApi;

/// <summary>Adds document metadata and the three authentication transports accepted by v1.</summary>
public sealed class SecuritySchemeTransformer : IOpenApiDocumentTransformer
{
    public const string BearerScheme = "bearer";
    public const string CookieScheme = "cookie";
    public const string ApiKeyScheme = "apiKey";

    public Task TransformAsync(
        OpenApiDocument document,
        OpenApiDocumentTransformerContext context,
        CancellationToken cancellationToken)
    {
        var cookieOptions = context.ApplicationServices
            .GetRequiredService<IOptions<AuthCookieOptions>>()
            .Value;

        document.Info = new OpenApiInfo
        {
            Title = "dotnet-web-api-startpack",
            Version = context.DocumentName,
            Description = "Authentication and authorization REST API.",
            Contact = new OpenApiContact { Name = "Project maintainers" },
            License = new OpenApiLicense { Name = "License not yet selected" },
        };

        document.Servers = [new OpenApiServer { Url = "/" }];
        document.Components ??= new OpenApiComponents();
        document.Components.SecuritySchemes = new Dictionary<string, IOpenApiSecurityScheme>
        {
            [BearerScheme] = new OpenApiSecurityScheme
            {
                Type = SecuritySchemeType.Http,
                Scheme = "bearer",
                BearerFormat = "JWT",
                Description = "ES256 access token in Authorization: Bearer <token>.",
            },
            [CookieScheme] = new OpenApiSecurityScheme
            {
                Type = SecuritySchemeType.ApiKey,
                In = ParameterLocation.Cookie,
                Name = cookieOptions.AccessCookieName,
                Description = "HTTP-only access-token cookie. State-changing requests also require the CSRF header.",
            },
            [ApiKeyScheme] = new OpenApiSecurityScheme
            {
                Type = SecuritySchemeType.ApiKey,
                In = ParameterLocation.Header,
                Name = "Authorization",
                Description = "API key as Authorization: ApiKey ak_<prefix>_<secret>.",
            },
        };

        return Task.CompletedTask;
    }
}
