using System.Net.Http.Json;
using System.Text.Json;
using Api.Models.Enums;
using Api.Services.Security;
using Api.Services.Tokens;
using IntegrationTests.Infrastructure;

namespace IntegrationTests.Security;

[Collection(IntegrationTestCollection.Name)]
[Trait("Category", "Security")]
public sealed class CsrfAttackTests(IntegrationTestFactory factory)
{
    [Fact]
    public async Task CookieStateChange_RequiresMatchingTokenBoundToAuthenticatedSession()
    {
        await factory.ResetDatabaseAsync();
        factory.Clock.Advance(TimeSpan.FromTicks(1));
        var firstSession = Guid.CreateVersion7();
        var secondSession = Guid.CreateVersion7();
        var accessToken = await IssueTokenAsync(firstSession);
        var tokens = await factory.InScopeAsync(services =>
        {
            var csrf = services.GetRequiredService<ICsrfTokenService>();
            return Task.FromResult((
                First: csrf.Issue(firstSession),
                Second: csrf.Issue(secondSession)));
        });

        var missing = await SendCookieLogoutAsync(accessToken, tokens.First, header: null);
        var wrong = await SendCookieLogoutAsync(accessToken, tokens.First, "wrong");
        var crossSession = await SendCookieLogoutAsync(accessToken, tokens.Second, tokens.Second);
        var accepted = await SendCookieLogoutAsync(accessToken, tokens.First, tokens.First);

        Assert.Equal(HttpStatusCode.Forbidden, missing.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, wrong.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, crossSession.StatusCode);
        Assert.Equal(HttpStatusCode.NotImplemented, accepted.StatusCode);

        var problem = await crossSession.Content.ReadFromJsonAsync<JsonElement>(
            TestContext.Current.CancellationToken);
        Assert.Equal("csrf_validation_failed", problem.GetProperty("errorCode").GetString());
    }

    private Task<HttpResponseMessage> SendCookieLogoutAsync(
        string accessToken,
        string csrfCookie,
        string? header)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/logout");
        request.Headers.Add(
            "Cookie",
            $"__Host-auth.access={accessToken}; __Host-auth.csrf={csrfCookie}");

        if (header is not null)
        {
            request.Headers.Add("X-CSRF-Token", header);
        }

        return factory.CreateClient().SendAsync(request, TestContext.Current.CancellationToken);
    }

    private async Task<string> IssueTokenAsync(Guid sessionId)
    {
        var issued = await factory.InScopeAsync(services =>
            services.GetRequiredService<IAccessTokenIssuer>().IssueAsync(
                new AccessTokenRequest
                {
                    UserId = Guid.CreateVersion7(),
                    SessionId = sessionId,
                    EmailVerified = true,
                    Roles = ["User"],
                    AuthenticationMethods = [AuthenticationMethod.Password],
                    AuthenticatedAt = factory.Clock.GetUtcNow(),
                },
                TestContext.Current.CancellationToken));

        return issued.Value;
    }
}
