using FluentValidation;
using FluentValidation.Results;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace Api.Filters;

/// <summary>
/// Runs the FluentValidation validator for every action argument that has one, before the
/// action body executes.
/// </summary>
/// <remarks>
/// An explicit filter rather than the deprecated <c>FluentValidation.AspNetCore</c>
/// auto-validation package (ADR-0009). Two things that buys: async validators keep working,
/// and the failure shape is ours rather than whatever MVC's model-state pipeline produces.
/// <para>
/// Validators are resolved per request from <c>HttpContext.RequestServices</c>, not from an
/// injected root provider — they are scoped, and resolving a scoped service from the root
/// container is how a validator ends up holding a <c>DbContext</c> that outlives the request.
/// </para>
/// </remarks>
public sealed class ValidationFilter : IAsyncActionFilter
{
    /// <summary>
    /// Problem Details extension carrying the stable machine-readable code for each failed
    /// field. Messages are English prose and may be reworded; these codes are the contract
    /// a localised client branches on.
    /// </summary>
    public const string ErrorCodesExtension = "errorCodes";

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var failures = new List<ValidationFailure>();

        foreach (var argument in context.ActionArguments.Values)
        {
            if (argument is null)
            {
                continue;
            }

            var validatorType = typeof(IValidator<>).MakeGenericType(argument.GetType());

            // No validator for this argument type is normal — route parameters, ids and
            // primitives have none. §10's guard test is what ensures a *request DTO* never
            // reaches here unvalidated; silently skipping is correct at runtime.
            if (context.HttpContext.RequestServices.GetService(validatorType) is not IValidator validator)
            {
                continue;
            }

            var validationContext = new ValidationContext<object>(argument);

            var result = await validator.ValidateAsync(
                validationContext,
                context.HttpContext.RequestAborted);

            if (!result.IsValid)
            {
                failures.AddRange(result.Errors);
            }
        }

        if (failures.Count == 0)
        {
            await next();
            return;
        }

        context.Result = BuildProblemResult(failures);
    }

    private static BadRequestObjectResult BuildProblemResult(List<ValidationFailure> failures)
    {
        var messages = failures
            .GroupBy(failure => failure.PropertyName)
            .ToDictionary(
                group => group.Key,
                group => group.Select(failure => failure.ErrorMessage).ToArray());

        var codes = failures
            .GroupBy(failure => failure.PropertyName)
            .ToDictionary(
                group => group.Key,
                group => group.Select(failure => failure.ErrorCode).Distinct().ToArray());

        var problem = new ValidationProblemDetails(messages)
        {
            Status = StatusCodes.Status400BadRequest,
            Title = "One or more validation errors occurred.",

            // TODO §13: replace with this API's own type URI, once the Problem Details
            //           catalog exists. The RFC section is a correct placeholder, not a
            //           final answer — a client should be able to look this type up and
            //           find documentation for *this* error, not for the standard.
            Type = "https://datatracker.ietf.org/doc/html/rfc9457#section-3",
        };

        problem.Extensions[ErrorCodesExtension] = codes;

        return new BadRequestObjectResult(problem)
        {
            ContentTypes = { "application/problem+json" },
        };
    }
}
