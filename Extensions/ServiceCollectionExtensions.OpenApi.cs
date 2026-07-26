using Api.Configuration.OpenApi;

namespace Api.Extensions;

public static partial class ServiceCollectionExtensions
{
    /// <summary>Registers the v1 OpenAPI document and its code-derived transformers.</summary>
    public static IServiceCollection AddApiOpenApi(this IServiceCollection services)
    {
        services.AddOpenApi("v1", options =>
        {
            // Version-neutral discovery endpoints belong in every version document.
            options.ShouldInclude = description =>
                description.GroupName is null
                || string.Equals(description.GroupName, "v1", StringComparison.Ordinal);

            options.AddDocumentTransformer<SecuritySchemeTransformer>();
            options.AddOperationTransformer<AuthRequirementOperationTransformer>();
        });

        return services;
    }
}
