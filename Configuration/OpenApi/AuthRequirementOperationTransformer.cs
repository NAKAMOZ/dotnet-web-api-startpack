using Api.Attributes;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace Api.Configuration.OpenApi;

/// <summary>
/// Projects endpoint authorization metadata into OpenAPI. The attributes remain the source
/// of truth, so a permission change cannot leave a stale authentication badge behind.
/// </summary>
public sealed class AuthRequirementOperationTransformer : IOpenApiOperationTransformer
{
    public Task TransformAsync(
        OpenApiOperation operation,
        OpenApiOperationTransformerContext context,
        CancellationToken cancellationToken)
    {
        var metadata = context.Description.ActionDescriptor.EndpointMetadata;

        if (metadata.OfType<IAllowAnonymous>().Any())
        {
            operation.Security = [];
            return Task.CompletedTask;
        }

        var schemes = metadata.OfType<RequireRecentAuthAttribute>().Any()
            ? new[]
            {
                SecuritySchemeTransformer.BearerScheme,
                SecuritySchemeTransformer.CookieScheme,
            }
            : new[]
            {
                SecuritySchemeTransformer.BearerScheme,
                SecuritySchemeTransformer.CookieScheme,
                SecuritySchemeTransformer.ApiKeyScheme,
            };

        operation.Security =
        [
            .. schemes.Select(scheme => new OpenApiSecurityRequirement
            {
                [new OpenApiSecuritySchemeReference(scheme, context.Document, externalResource: null)] = [],
            }),
        ];

        return Task.CompletedTask;
    }
}
