using System.Net.Http.Json;

namespace IntegrationTests.Infrastructure;

public sealed class StubSocialHttpClientFactory : IHttpClientFactory
{
    private readonly HttpClient _client = new(new StubSocialBackchannelHandler());

    public HttpClient CreateClient(string name) => _client;

    private sealed class StubSocialBackchannelHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            HttpResponseMessage response;

            if (request.RequestUri?.AbsoluteUri == "https://oauth2.googleapis.com/token")
            {
                response = new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = JsonContent.Create(new { access_token = "stub-google-access-token" }),
                };
            }
            else if (request.RequestUri?.AbsoluteUri == "https://openidconnect.googleapis.com/v1/userinfo"
                     && request.Headers.Authorization?.Parameter == "stub-google-access-token")
            {
                response = new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = JsonContent.Create(new
                    {
                        sub = "google-subject-42",
                        email = "social-user@example.com",
                        email_verified = true,
                        name = "Social User",
                    }),
                };
            }
            else
            {
                response = new HttpResponseMessage(HttpStatusCode.BadGateway);
            }

            return Task.FromResult(response);
        }
    }
}
