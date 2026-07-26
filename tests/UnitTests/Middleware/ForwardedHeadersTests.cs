using System.Net;
using Api.Configuration;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace UnitTests.Middleware;

public sealed class ForwardedHeadersTests
{
    [Fact]
    public async Task KnownProxy_ControlsClientAddressAndScheme()
    {
        var middleware = CreateMiddleware("10.0.0.10");
        var context = CreateContext("10.0.0.10");

        await middleware.Invoke(context);

        Assert.Equal(IPAddress.Parse("203.0.113.42"), context.Connection.RemoteIpAddress);
        Assert.Equal("https", context.Request.Scheme);
    }

    [Fact]
    public async Task UnknownProxy_CannotForgeClientAddressOrScheme()
    {
        var middleware = CreateMiddleware("10.0.0.10");
        var context = CreateContext("10.0.0.99");

        await middleware.Invoke(context);

        Assert.Equal(IPAddress.Parse("10.0.0.99"), context.Connection.RemoteIpAddress);
        Assert.Equal("http", context.Request.Scheme);
    }

    private static ForwardedHeadersMiddleware CreateMiddleware(string knownProxy)
    {
        var projected = new ForwardedHeadersOptions();
        new ConfigureForwardedHeadersOptions(
                Options.Create(new ReverseProxyOptions
                {
                    Enabled = true,
                    KnownProxies = [knownProxy],
                }))
            .Configure(projected);

        return new ForwardedHeadersMiddleware(
            _ => Task.CompletedTask,
            NullLoggerFactory.Instance,
            Options.Create(projected));
    }

    private static DefaultHttpContext CreateContext(string remoteAddress)
    {
        var context = new DefaultHttpContext();
        context.Connection.RemoteIpAddress = IPAddress.Parse(remoteAddress);
        context.Request.Scheme = "http";
        context.Request.Headers["X-Forwarded-For"] = "203.0.113.42";
        context.Request.Headers["X-Forwarded-Proto"] = "https";
        return context;
    }
}
