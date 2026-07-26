using Api.Configuration;
using Api.DTOs.Auth;
using Api.DTOs.Common;
using Api.Services.Auth;
using Api.Services.Email;
using Api.Services.Security;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace UnitTests.Services;

public sealed class FeatureServiceTests
{
    [Fact]
    public void EmailTemplateRenderer_HtmlEncodesSubstitutedValues()
    {
        var renderer = new EmbeddedEmailTemplateRenderer();

        var rendered = renderer.Render(
            "EmailVerification",
            new Dictionary<string, string> { ["Token"] = "<script>&token" });

        Assert.Contains("&lt;script&gt;&amp;token", rendered, StringComparison.Ordinal);
        Assert.DoesNotContain("<script>", rendered, StringComparison.Ordinal);
    }

    [Fact]
    public void AuthTokenTransport_BodyModeReturnsTokensAndWritesNoCookies()
    {
        var context = new DefaultHttpContext();
        var transport = CreateTransport(context);

        var delivered = transport.DeliverLogin(
            Login(),
            Guid.CreateVersion7(),
            "access-value",
            "refresh-value");

        Assert.Equal("access-value", delivered.AccessToken);
        Assert.Equal("refresh-value", delivered.RefreshToken);
        Assert.Equal(0, context.Response.Headers.SetCookie.Count);
    }

    [Fact]
    public void AuthTokenTransport_CookieModeWritesExclusiveCookieMatrix()
    {
        var context = new DefaultHttpContext();
        context.Request.Headers["X-Auth-Transport"] = "cookie";
        var csrf = Substitute.For<ICsrfTokenService>();
        csrf.Issue(Arg.Any<Guid>()).Returns("csrf-value");
        var transport = CreateTransport(context, csrf);

        var delivered = transport.DeliverLogin(
            Login(),
            Guid.CreateVersion7(),
            "access-value",
            "refresh-value");

        Assert.Null(delivered.AccessToken);
        Assert.Null(delivered.RefreshToken);
        var cookies = context.Response.Headers.SetCookie.ToArray();
        Assert.Equal(3, cookies.Length);
        Assert.Contains(cookies, value =>
            value!.StartsWith("__Host-auth.access=access-value", StringComparison.Ordinal)
            && value.Contains("httponly", StringComparison.OrdinalIgnoreCase)
            && value.Contains("samesite=lax", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(cookies, value =>
            value!.StartsWith("__Secure-auth.refresh=refresh-value", StringComparison.Ordinal)
            && value.Contains("path=/api/v1/auth/refresh", StringComparison.OrdinalIgnoreCase)
            && value.Contains("samesite=strict", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(cookies, value =>
            value!.StartsWith("__Host-auth.csrf=csrf-value", StringComparison.Ordinal)
            && !value.Contains("httponly", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void AuthTokenTransport_ExistingRefreshCookieDoesNotOverrideBodyLogin()
    {
        var context = new DefaultHttpContext();
        context.Request.Headers.Cookie = "__Secure-auth.refresh=old-refresh-value";
        var transport = CreateTransport(context);

        var delivered = transport.DeliverLogin(
            Login(),
            Guid.CreateVersion7(),
            "access-value",
            "refresh-value");

        Assert.Equal("access-value", delivered.AccessToken);
        Assert.Equal("refresh-value", delivered.RefreshToken);
        Assert.Equal(0, context.Response.Headers.SetCookie.Count);
    }

    [Fact]
    public void AuthTokenTransport_CookieRefreshKeepsRotatedTokensInCookies()
    {
        var context = new DefaultHttpContext();
        context.Request.Headers.Cookie = "__Secure-auth.refresh=old-refresh-value";
        var csrf = Substitute.For<ICsrfTokenService>();
        csrf.Issue(Arg.Any<Guid>()).Returns("csrf-value");
        var transport = CreateTransport(context, csrf);

        var delivered = transport.DeliverRefresh(
            new TokenPairResponse
            {
                TokenType = "Bearer",
                ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(15),
            },
            Guid.CreateVersion7(),
            "access-value",
            "refresh-value");

        Assert.Null(delivered.AccessToken);
        Assert.Null(delivered.RefreshToken);
        Assert.Equal(3, context.Response.Headers.SetCookie.Count);
    }

    [Fact]
    public void AuthTokenTransport_ConflictingBodyAndCookieRefreshTokensAreRejected()
    {
        var context = new DefaultHttpContext();
        context.Request.Headers.Cookie = "__Secure-auth.refresh=cookie-value";
        var transport = CreateTransport(context);

        Assert.Null(transport.ReadRefreshToken("different-body-value"));
    }

    private static AuthTokenTransport CreateTransport(
        HttpContext context,
        ICsrfTokenService? csrf = null)
    {
        var accessor = new HttpContextAccessor { HttpContext = context };
        return new AuthTokenTransport(
            accessor,
            csrf ?? Substitute.For<ICsrfTokenService>(),
            Options.Create(new AuthCookieOptions { RequireSecure = false }));
    }

    private static LoginResponse Login() =>
        new()
        {
            TokenType = "Bearer",
            ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(15),
            User = new UserSummary
            {
                Id = Guid.CreateVersion7(),
                Email = "unit@example.com",
                EmailVerified = true,
                Roles = ["User"],
            },
        };
}
