using System.Net.Http.Json;
using System.Text.Json;
using Api.Configuration;
using Api.Services.Security;
using IntegrationTests.Infrastructure;
using Microsoft.Extensions.Options;

namespace IntegrationTests.Security;

[Collection(IntegrationTestCollection.Name)]
[Trait("Category", "Security")]
public sealed class CsrfAttackTests(IntegrationTestFactory factory)
{
    [Fact]
    public async Task CookieStateChange_RequiresMatchingTokenBoundToAuthenticatedSession()
    {
        await factory.ResetAsync();
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
        // Read from the same options the server reads. A literal here is the third copy of a
        // name AuthCookieDefaults exists to keep singular.
        var cookies = factory.Services.GetRequiredService<IOptions<AuthCookieOptions>>().Value;
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/logout");
        request.Headers.Add(
            "Cookie",
            $"{cookies.AccessCookieName}={accessToken}; {cookies.CsrfCookieName}={csrfCookie}");

        if (header is not null)
        {
            request.Headers.Add(cookies.CsrfHeaderName, header);
        }

        return factory.CreateClient().SendAsync(request, TestContext.Current.CancellationToken);
    }

    private Task<string> IssueTokenAsync(Guid sessionId) =>
        factory.IssueAccessTokenAsync(
            Guid.CreateVersion7(),
            sessionId,
            TestContext.Current.CancellationToken);
}
