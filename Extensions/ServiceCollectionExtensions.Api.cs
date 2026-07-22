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
        services.AddControllers();

        // RFC 9457 Problem Details for every error response (ADR-0007).
        // §13 customises the payload; this registers the default writer.
        services.AddProblemDetails();

        // Built-in OpenAPI generator. Scalar renders it in §18.
        services.AddOpenApi();

        // TODO §11: URL-segment versioning via Asp.Versioning.Mvc (ADR-0015),
        //           JSON options (camelCase, enums as strings, ignore-null).
        return services;
    }
}
