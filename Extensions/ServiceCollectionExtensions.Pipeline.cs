using Api.Configuration;
using Api.Filters;
using Api.Handlers.Cors;
using Api.Middleware;
using Api.Services.Security;
using Microsoft.AspNetCore.Cors.Infrastructure;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Api.Extensions;

public static partial class ServiceCollectionExtensions
{
    /// <summary>
    /// The registrations the request pipeline needs: exception handling, CORS and CSRF (§14).
    /// </summary>
    /// <remarks>
    /// Called from <see cref="AddApiServices"/> rather than from <c>Program.cs</c>. The
    /// composition root stays sixteen lines, and — more usefully — the pipeline cannot be
    /// assembled without its services, because forgetting one of these produces a startup
    /// failure rather than a middleware that quietly does nothing.
    /// </remarks>
    public static IServiceCollection AddPipelineServices(this IServiceCollection services)
    {
        // The handler UseExceptionHandler() dispatches to. Registered as IExceptionHandler,
        // so the framework owns the ordering and the re-throw when no handler claims an
        // exception (§13's map decides what each one becomes).
        services.AddExceptionHandler<ExceptionHandlingMiddleware>();

        services
            .AddOptions<ApiCorsOptions>()
            .BindConfiguration(ApiCorsOptions.SectionName)
            .ValidateOnStart();

        // AddCors registers the CORS services and the default policy provider; the line
        // after replaces that provider. Both are needed — the middleware resolves several
        // other services from AddCors.
        services.AddCors();
        services.Replace(ServiceDescriptor.Singleton<ICorsPolicyProvider, OriginAwareCorsPolicyProvider>());

        // Singleton: it holds one Data Protection protector and no per-request state.
        services.TryAddSingleton<ICsrfTokenService, CsrfTokenService>();

        // Global, like the validation filter. A CSRF filter applied per controller is one
        // that a new controller forgets — and the omission is invisible until someone
        // exploits it. Requests it does not apply to (safe methods, bearer credentials) are
        // exempted inside the filter, where the reasoning is written down.
        services.Configure<MvcOptions>(options => options.Filters.Add<CsrfProtectionFilter>());

        // §14's deferred filter, landed with §15 now that IAuditLogger exists. Global for the
        // same reason as the two above, and inert on any action without [AuditEvent].
        services.Configure<MvcOptions>(options => options.Filters.Add<AuditActionFilter>());

        return services;
    }
}
