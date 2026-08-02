using System.Net.Http.Json;
using Api.Data;
using Api.DTOs.Auth;
using Api.DTOs.SocialAuth;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;

namespace IntegrationTests;

[Collection(IntegrationTestCollection.Name)]
public sealed class SocialAuthIntegrationTests(IntegrationTestFactory factory)
{
    [Fact]
    public async Task GoogleBackchannel_CreatesOnceThenReusesProviderSubjectAndStateIsSingleUse()
    {
        await factory.ResetAsync();
        var cancellationToken = TestContext.Current.CancellationToken;
        var client = factory.CreateClient();

        var firstState = await AuthorizeStateAsync(client, cancellationToken);
        var first = await client.GetAsync(
            $"/api/v1/auth/social/google/callback?code=provider-code&state={Uri.EscapeDataString(firstState)}",
            cancellationToken);
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.NotNull((await first.Content.ReadFromJsonAsync<LoginResponse>(cancellationToken))!.AccessToken);

        var replay = await client.GetAsync(
            $"/api/v1/auth/social/google/callback?code=provider-code&state={Uri.EscapeDataString(firstState)}",
            cancellationToken);
        Assert.Equal(HttpStatusCode.BadRequest, replay.StatusCode);

        var secondState = await AuthorizeStateAsync(client, cancellationToken);
        var second = await client.GetAsync(
            $"/api/v1/auth/social/google/callback?code=provider-code&state={Uri.EscapeDataString(secondState)}",
            cancellationToken);
        Assert.Equal(HttpStatusCode.OK, second.StatusCode);

        await factory.InScopeAsync(async services =>
        {
            var database = services.GetRequiredService<AppDbContext>();
            Assert.Equal(1, await database.Accounts.CountAsync(
                account => account.Provider == "google"
                           && account.ProviderAccountId == "google-subject-42",
                cancellationToken));
            Assert.Equal(1, await database.Users.CountAsync(
                user => user.Email == "social-user@example.com",
                cancellationToken));
            Assert.Equal(2, await database.Sessions.CountAsync(cancellationToken));
        });
    }

    private static async Task<string> AuthorizeStateAsync(
        HttpClient client,
        CancellationToken cancellationToken)
    {
        var authorize = await client.GetFromJsonAsync<SocialAuthorizeResponse>(
            "/api/v1/auth/social/google/authorize",
            cancellationToken);
        var query = QueryHelpers.ParseQuery(new Uri(authorize!.AuthorizationUrl).Query);
        return query.TryGetValue("state", out var state)
            ? state.ToString()
            : throw new InvalidOperationException("Authorize URL did not contain state.");
    }
}
