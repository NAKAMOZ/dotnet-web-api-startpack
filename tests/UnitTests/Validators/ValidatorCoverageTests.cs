using System.Reflection;
using Api.DTOs.Common;
using Api.Extensions;
using Api.Filters;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;

namespace UnitTests.Validators;

/// <summary>
/// The §10 coverage guard: no request DTO may exist without a validator (§10 DoD).
/// </summary>
/// <remarks>
/// This is the test that makes "validate everything" true rather than aspirational. A new
/// endpoint arrives with a new request record, and the validator is the easiest thing to
/// forget — the endpoint works perfectly in every manual test, because the developer sends
/// well-formed input. The gap only shows when someone sends something else.
/// </remarks>
public class ValidatorCoverageTests
{
    private static readonly Assembly ApiAssembly = typeof(ValidationFilter).Assembly;

    /// <summary>
    /// Types that legitimately have no validator of their own.
    /// </summary>
    private static readonly Type[] Exempt =
    [
        // A base type, never bound to an action directly. Its rules are applied by the
        // derived query validators through ApplyPagingRules.
        typeof(PagedQuery),
    ];

    private static IEnumerable<Type> RequestDtoTypes =>
        ApiAssembly.GetTypes()
            .Where(type => type.Namespace?.StartsWith("Api.DTOs", StringComparison.Ordinal) == true)
            .Where(type => type is { IsClass: true, IsAbstract: false })
            .Where(type => type.Name.EndsWith("Request", StringComparison.Ordinal)
                           || type.Name.EndsWith("Query", StringComparison.Ordinal))
            .Where(type => !Exempt.Contains(type));

    [Fact]
    public void EveryRequestDtoHasAValidator()
    {
        var validatedTypes = ApiAssembly.GetTypes()
            .Where(type => type is { IsClass: true, IsAbstract: false })
            .SelectMany(type => type.GetInterfaces())
            .Where(@interface => @interface.IsGenericType
                                 && @interface.GetGenericTypeDefinition() == typeof(IValidator<>))
            .Select(@interface => @interface.GetGenericArguments()[0])
            .ToHashSet();

        var missing = RequestDtoTypes
            .Where(type => !validatedTypes.Contains(type))
            .Select(type => type.FullName!)
            .ToArray();

        Assert.Empty(missing);
    }

    [Fact]
    public void EveryValidatorResolvesFromTheContainer()
    {
        // A validator that cannot be constructed is worse than a missing one: the endpoint
        // fails at request time with a DI error rather than at build or boot. This catches
        // the case where a validator takes a dependency nothing registers — the API-key
        // validator's TimeProvider is exactly that shape.
        var services = new ServiceCollection().AddValidationServices().BuildServiceProvider();

        using var scope = services.CreateScope();

        var unresolvable = RequestDtoTypes
            .Where(type => scope.ServiceProvider.GetService(typeof(IValidator<>).MakeGenericType(type)) is null)
            .Select(type => type.FullName!)
            .ToArray();

        Assert.Empty(unresolvable);
    }

    [Fact]
    public void EveryRuleCarriesAStableErrorCode()
    {
        // FluentValidation falls back to the rule's class name ("NotEmptyValidator") when no
        // code is set. That is a leaked implementation detail, and it changes whenever the
        // rule does — so a client keying on it breaks silently on a library upgrade.
        var services = new ServiceCollection().AddValidationServices().BuildServiceProvider();

        using var scope = services.CreateScope();

        var violations = new List<string>();

        foreach (var dtoType in RequestDtoTypes)
        {
            var validator = (IValidator)scope.ServiceProvider
                .GetRequiredService(typeof(IValidator<>).MakeGenericType(dtoType));

            var descriptor = validator.CreateDescriptor();

            violations.AddRange(
                from rule in descriptor.Rules
                from component in rule.Components
                where component.ErrorCode is null || component.ErrorCode.EndsWith("Validator", StringComparison.Ordinal)
                select $"{validator.GetType().Name}.{rule.PropertyName}: {component.ErrorCode ?? "(none)"}");
        }

        Assert.Empty(violations);
    }
}
