using Api.Filters;
using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Api.Extensions;

public static partial class ServiceCollectionExtensions
{
    /// <summary>
    /// FluentValidation validators and the validation action filter.
    /// </summary>
    public static IServiceCollection AddValidationServices(this IServiceCollection services)
    {
        services.TryAddSingleton(TimeProvider.System);

        // Assembly scan, so adding a validator file is the whole job — no registration line
        // to forget. Scoped, because a validator may take request-scoped dependencies.
        //
        // includeInternalTypes is required and easy to miss: the scanner registers only
        // public validators by default, and every validator here is internal (nothing
        // outside the assembly constructs one). Without it the scan finds nothing, no
        // validator resolves, and the filter skips every argument — validation silently
        // stops happening while every endpoint keeps returning 200.
        services.AddValidatorsFromAssemblyContaining<ValidationFilter>(
            ServiceLifetime.Scoped,
            includeInternalTypes: true);

        // Added through MvcOptions rather than to AddControllers, so this method stays
        // independent of the order Program.cs calls the Add* extensions in.
        services.Configure<MvcOptions>(options => options.Filters.Add<ValidationFilter>());

        // MVC's built-in model-state filter would answer first, with a different body shape,
        // for anything DataAnnotations or the binder rejects. Suppressing it makes the
        // validation filter the single producer of 400s — one shape, one set of error codes.
        //
        // The cost is that malformed JSON and unbindable values now fall through to the
        // exception handler instead of MVC's automatic response. §13 and §14 own that path;
        // until they land, those requests produce an unshaped 400.
        services.Configure<ApiBehaviorOptions>(options => options.SuppressModelStateInvalidFilter = true);

        return services;
    }
}
