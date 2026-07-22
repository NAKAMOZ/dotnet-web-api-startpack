using System.Text.Json.Serialization;
using Asp.Versioning;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Api.Extensions;

/// <summary>
/// Composition-root registrations, split by concern across
/// <c>ServiceCollectionExtensions.*.cs</c>. <c>Program.cs</c> may only call these
/// methods — it never registers a service directly (ADR-0007).
/// </summary>
public static partial class ServiceCollectionExtensions
{
    /// <summary>
    /// MVC controllers, API versioning, JSON options and OpenAPI document generation.
    /// </summary>
    public static IServiceCollection AddApiServices(this IServiceCollection services)
    {
        // Every component that reads the clock takes TimeProvider rather than calling
        // DateTimeOffset.UtcNow, so session expiry, token TTLs, lockout windows and the
        // step-up window are testable without real waiting (ADR-0011). Tests substitute
        // FakeTimeProvider.
        services.TryAddSingleton(TimeProvider.System);

        // Controllers only — no minimal API endpoints anywhere in this project (ADR-0007).
        services
            .AddControllers()
            .AddJsonOptions(options =>
            {
                options.JsonSerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;

                // Enums cross the wire as their names. An ordinal is meaningless to a client
                // and silently re-points every existing value when a member is inserted —
                // the same reasoning that governs how they are stored (DataAccess.md §2).
                options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());

                // Nulls are omitted. The token fields in cookie mode are the reason this is
                // a global policy rather than a per-property attribute: a response that
                // carries "accessToken": null invites a client to store it.
                options.JsonSerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
            });

        // RFC 9457 Problem Details for every error response (ADR-0007).
        // §13 customises the payload; this registers the default writer.
        services.AddProblemDetails();

        // URL-segment versioning: /api/v1/… (P2, ADR-0015).
        services
            .AddApiVersioning(options =>
            {
                options.DefaultApiVersion = new ApiVersion(1, 0);
                options.AssumeDefaultVersionWhenUnspecified = true;

                // Advertises supported and deprecated versions in response headers, so a
                // client learns a version is going away from its own traffic rather than
                // from a changelog it never reads.
                options.ReportApiVersions = true;
            })
            .AddApiExplorer(options =>
            {
                // "v1", not "1.0" — the group name is what lands in the route and in the
                // OpenAPI document Scalar renders (§18).
                options.GroupNameFormat = "'v'V";
                options.SubstituteApiVersionInUrl = true;
            });

        // Built-in OpenAPI generator. Scalar renders it in §18.
        services.AddOpenApi();

        return services;
    }
}
