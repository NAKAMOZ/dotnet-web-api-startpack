using System.Security.Claims;
using Api.Logging;
using Api.Middleware;
using Microsoft.AspNetCore.Http;
using Serilog;
using Serilog.Core;
using Serilog.Events;

namespace UnitTests.Logging;

/// <summary>
/// The two request-scoped enrichers (§15): what they attach, and — as importantly — when they
/// attach nothing.
/// </summary>
public class EnricherTests
{
    [Fact]
    public void TheCorrelationIdIsAttachedFromHttpContextItems()
    {
        var context = new DefaultHttpContext();
        context.Items[CorrelationId.ItemsKey] = "order-4711";

        var properties = Emit(new CorrelationIdEnricher(AccessorFor(context)));

        Assert.Equal("order-4711", Scalar(properties, CorrelationIdEnricher.PropertyName));
    }

    [Fact]
    public void NoHttpContextMeansNoCorrelationId()
    {
        // Startup lines and §12's background worker. An id that matches no request is worse
        // than an absent one — it sends an investigation to the wrong place.
        var properties = Emit(new CorrelationIdEnricher(AccessorFor(httpContext: null)));

        Assert.DoesNotContain(CorrelationIdEnricher.PropertyName, properties.Keys);
    }

    [Fact]
    public void AnExplicitlyLoggedCorrelationIdIsNotOverwritten()
    {
        var context = new DefaultHttpContext();
        context.Items[CorrelationId.ItemsKey] = "ambient";

        var properties = Emit(
            new CorrelationIdEnricher(AccessorFor(context)),
            "replaying {CorrelationId}",
            "explicit");

        Assert.Equal("explicit", Scalar(properties, CorrelationIdEnricher.PropertyName));
    }

    [Fact]
    public void TheUserIdIsAttachedWhenAuthenticated()
    {
        var userId = Guid.NewGuid();

        var properties = Emit(new UserIdEnricher(AccessorFor(AuthenticatedAs(ClaimTypes.NameIdentifier, userId))));

        Assert.Equal(userId.ToString(), Scalar(properties, UserIdEnricher.PropertyName));
    }

    [Fact]
    public void TheShortSubClaimIsAlsoRead()
    {
        // The API-key handler issues `sub` directly rather than the mapped long name. Reading
        // only ClaimTypes.NameIdentifier would silently lose every API-key request.
        var userId = Guid.NewGuid();

        var properties = Emit(new UserIdEnricher(AccessorFor(AuthenticatedAs("sub", userId))));

        Assert.Equal(userId.ToString(), Scalar(properties, UserIdEnricher.PropertyName));
    }

    [Fact]
    public void AnAnonymousRequestCarriesNoUserId()
    {
        var properties = Emit(new UserIdEnricher(AccessorFor(new DefaultHttpContext())));

        Assert.DoesNotContain(UserIdEnricher.PropertyName, properties.Keys);
    }

    private static DefaultHttpContext AuthenticatedAs(string claimType, Guid userId) =>
        new()
        {
            User = new ClaimsPrincipal(
                new ClaimsIdentity([new Claim(claimType, userId.ToString())], authenticationType: "Test")),
        };

    private static IHttpContextAccessor AccessorFor(HttpContext? httpContext) =>
        new HttpContextAccessor { HttpContext = httpContext };

    private static IReadOnlyDictionary<string, LogEventPropertyValue> Emit(
        ILogEventEnricher enricher,
        string messageTemplate = "anything",
        params object[] arguments)
    {
        var sink = new CollectingSink();

        using var logger = new LoggerConfiguration()
            .Enrich.With(enricher)
            .WriteTo.Sink(sink)
            .CreateLogger();

        logger.Information(messageTemplate, arguments);

        return Assert.Single(sink.Events).Properties;
    }

    private static string? Scalar(IReadOnlyDictionary<string, LogEventPropertyValue> properties, string name) =>
        (properties[name] as ScalarValue)?.Value?.ToString();
}
