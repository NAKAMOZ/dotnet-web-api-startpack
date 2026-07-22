namespace Api.Extensions;

public static partial class ServiceCollectionExtensions
{
    /// <summary>
    /// FluentValidation validators and the validation action filter.
    /// </summary>
    public static IServiceCollection AddValidationServices(this IServiceCollection services)
    {
        // TODO §10: assembly-scan registration of IValidator<T> implementations from
        //           Validators/ (ADR-0009), plus the validation filter that converts
        //           failures to Problem Details. The deprecated FluentValidation.AspNetCore
        //           auto-validation package is deliberately NOT used.
        return services;
    }
}
