using Api.Exceptions;
using Api.Middleware;
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
        // Binding failures first, and they must be handled here explicitly.
        //
        // §10 suppressed MVC's automatic model-state filter so this filter is the single
        // producer of 400s. The consequence, found live: a body that fails to deserialize —
        // `{}` against a record with `required` members, a string where a Guid belongs —
        // leaves a NULL action argument. This filter would then find nothing to validate,
        // the action would run with a null model, and the caller would get a misleading
        // success-shaped response instead of "your request was malformed".
        if (!context.ModelState.IsValid)
        {
            context.Result = BuildMalformedRequestResult(context);
            return;
        }

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

        context.Result = BuildProblemResult(failures, context.HttpContext);
    }

    /// <summary>
    /// A 400 for a request the model binder could not read.
    /// </summary>
    /// <remarks>
    /// Field names are reported; the binder's messages are not. Those messages name CLR
    /// types and JSON paths, which describes our model to whoever is probing it.
    /// </remarks>
    private static BadRequestObjectResult BuildMalformedRequestResult(ActionExecutingContext context)
    {
        var fields = context.ModelState
            .Where(entry => entry.Value?.Errors.Count > 0)
            .Select(entry => entry.Key)
            .Where(key => !string.IsNullOrEmpty(key))
            .ToArray();

        var problem = new ProblemDetails
        {
            Status = StatusCodes.Status400BadRequest,
            Title = "The request could not be read.",
            Type = ProblemTypes.For(Api.Exceptions.ErrorCodes.MalformedRequest),
            Detail = fields.Length > 0
                ? $"Could not bind: {string.Join(", ", fields)}."
                : "The request body could not be parsed.",
        };

        problem.Extensions[ProblemDetailsExtensions.ErrorCode] = Api.Exceptions.ErrorCodes.MalformedRequest;
        problem.Extensions[ProblemDetailsExtensions.TraceId] = context.HttpContext.TraceIdentifier;

        return new BadRequestObjectResult(problem) { ContentTypes = { "application/problem+json" } };
    }

    private static BadRequestObjectResult BuildProblemResult(
        List<ValidationFailure> failures,
        HttpContext httpContext)
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
            Type = ProblemTypes.For(Api.Exceptions.ErrorCodes.ValidationFailed),
        };

        // Two levels, and both are needed. The top-level code says "this request failed
        // validation"; the per-field codes say which rule each field broke. A client
        // branches on the first to decide how to react and reads the second to localise.
        problem.Extensions[ProblemDetailsExtensions.ErrorCode] = Api.Exceptions.ErrorCodes.ValidationFailed;
        problem.Extensions[ProblemDetailsExtensions.ErrorCodes] = codes;

        // Set here rather than left to CustomizeProblemDetails. That callback runs through
        // IProblemDetailsService, and a result written straight to the response by an action
        // filter never reaches it — so without these two lines a validation failure would be
        // the one error response in the API with no correlation id to trace it by.
        problem.Extensions[ProblemDetailsExtensions.TraceId] = httpContext.TraceIdentifier;

        if (httpContext.Items.TryGetValue(CorrelationId.ItemsKey, out var correlationId))
        {
            problem.Extensions[ProblemDetailsExtensions.CorrelationId] = correlationId;
        }

        return new BadRequestObjectResult(problem)
        {
            ContentTypes = { "application/problem+json" },
        };
    }
}
