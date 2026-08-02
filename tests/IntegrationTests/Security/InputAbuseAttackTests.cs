using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Api.DTOs.Auth;

namespace IntegrationTests.Security;

[Collection(IntegrationTestCollection.Name)]
[Trait("Category", "Security")]
public sealed class InputAbuseAttackTests(IntegrationTestFactory factory)
{
    [Fact]
    public async Task OversizedBody_IsRejectedWithProblemDetailsBeforeParsing()
    {
        await factory.ResetAsync();
        var body = JsonSerializer.Serialize(new
        {
            email = "attacker@example.com",
            password = new string('x', 70_000),
        });
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/login")
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json"),
        };

        var response = await factory.CreateClient().SendAsync(
            request,
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.RequestEntityTooLarge, response.StatusCode);
        Assert.Equal("payload_too_large", await ErrorCodeAsync(response));
    }

    [Fact]
    public async Task MalformedJson_IsRejectedWithStableProblemDetails()
    {
        await factory.ResetAsync();
        using var content = new StringContent(
            "{\"email\": \"user@example.com\", \"password\": ",
            Encoding.UTF8,
            "application/json");

        var response = await factory.CreateClient().PostAsync(
            "/api/v1/auth/login",
            content,
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("malformed_request", await ErrorCodeAsync(response));
    }

    private static async Task<string?> ErrorCodeAsync(HttpResponseMessage response)
    {
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>(
            TestContext.Current.CancellationToken);
        return problem.GetProperty("errorCode").GetString();
    }
}
