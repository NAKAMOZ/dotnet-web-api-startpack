using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Api.Data;
using Api.DTOs.Auth;
using Api.DTOs.Passkeys;
using Api.Models.Enums;
using Microsoft.EntityFrameworkCore;

namespace IntegrationTests;

[Collection(IntegrationTestCollection.Name)]
public sealed class PasskeyCeremonyIntegrationTests(IntegrationTestFactory factory)
{
    [Fact]
    public async Task SoftwareAuthenticator_RegistersAuthenticatesRejectsReplayAndDeletes()
    {
        await factory.ResetAsync();
        factory.EmailSender.Clear();
        var cancellationToken = TestContext.Current.CancellationToken;
        var userId = await factory.SeedUserAsync(cancellationToken);
        var bearer = await factory.IssueAccessTokenAsync(
            userId,
            Guid.CreateVersion7(),
            cancellationToken);
        var authenticated = factory.CreateClient();
        authenticated.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", bearer);
        using var authenticator = new SoftwareWebAuthnAuthenticator();

        var registrationOptionsResponse = await authenticated.PostAsJsonAsync(
            "/api/v1/passkeys/registration/options",
            new PasskeyRegistrationOptionsRequest { Label = "Virtual platform key" },
            cancellationToken);
        Assert.Equal(HttpStatusCode.OK, registrationOptionsResponse.StatusCode);
        var registrationOptions = await registrationOptionsResponse.Content
            .ReadFromJsonAsync<PasskeyRegistrationOptionsResponse>(cancellationToken);
        var attestation = authenticator.CreateAttestation(registrationOptions!.Options);

        var registration = await authenticated.PostAsJsonAsync(
            "/api/v1/passkeys/registration/complete",
            new PasskeyRegistrationRequest
            {
                AttestationResponse = attestation,
                Label = "Virtual platform key",
            },
            cancellationToken);
        Assert.Equal(HttpStatusCode.Created, registration.StatusCode);
        var credential = await registration.Content.ReadFromJsonAsync<PasskeyResponse>(cancellationToken);
        Assert.Equal(authenticator.CredentialId, credential!.CredentialId);

        var registrationReplay = await authenticated.PostAsJsonAsync(
            "/api/v1/passkeys/registration/complete",
            new PasskeyRegistrationRequest { AttestationResponse = attestation },
            cancellationToken);
        Assert.Equal(HttpStatusCode.BadRequest, registrationReplay.StatusCode);

        var anonymous = factory.CreateClient();
        var assertionOptions = await GetAssertionOptionsAsync(anonymous, cancellationToken);
        var assertion = authenticator.CreateAssertion(assertionOptions, userId);
        var authentication = await anonymous.PostAsJsonAsync(
            "/api/v1/passkeys/authentication/complete",
            new PasskeyAuthenticationRequest { AssertionResponse = assertion },
            cancellationToken);
        Assert.Equal(HttpStatusCode.OK, authentication.StatusCode);
        var login = await authentication.Content.ReadFromJsonAsync<LoginResponse>(cancellationToken);
        Assert.NotNull(login?.AccessToken);
        Assert.Equal(userId, login.User.Id);

        var payload = ParseJwtPayload(login.AccessToken);
        Assert.Contains(
            "webauthn",
            payload.GetProperty("amr").EnumerateArray().Select(value => value.GetString()));

        var assertionReplay = await anonymous.PostAsJsonAsync(
            "/api/v1/passkeys/authentication/complete",
            new PasskeyAuthenticationRequest { AssertionResponse = assertion },
            cancellationToken);
        Assert.Equal(HttpStatusCode.Unauthorized, assertionReplay.StatusCode);

        var clonedCounterOptions = await GetAssertionOptionsAsync(anonymous, cancellationToken);
        var clonedCounterAssertion = authenticator.CreateAssertion(
            clonedCounterOptions,
            userId,
            advanceCounter: false);
        var clonedCounter = await anonymous.PostAsJsonAsync(
            "/api/v1/passkeys/authentication/complete",
            new PasskeyAuthenticationRequest { AssertionResponse = clonedCounterAssertion },
            cancellationToken);
        Assert.Equal(HttpStatusCode.Unauthorized, clonedCounter.StatusCode);

        var listed = await authenticated.GetFromJsonAsync<List<PasskeyResponse>>(
            "/api/v1/passkeys",
            cancellationToken);
        Assert.Equal(authenticator.CredentialId, Assert.Single(listed!).CredentialId);

        var deletion = await authenticated.DeleteAsync(
            $"/api/v1/passkeys/{authenticator.CredentialId}",
            cancellationToken);
        Assert.Equal(HttpStatusCode.NoContent, deletion.StatusCode);
        Assert.Empty(await authenticated.GetFromJsonAsync<List<PasskeyResponse>>(
            "/api/v1/passkeys",
            cancellationToken) ?? []);

        await factory.InScopeAsync(async services =>
        {
            var database = services.GetRequiredService<AppDbContext>();
            Assert.True(await database.AuditLogEntries.AnyAsync(
                entry => entry.UserId == userId
                         && entry.EventType == AuditEventType.LoginSucceeded,
                cancellationToken));
        });
    }

    private static async Task<JsonElement> GetAssertionOptionsAsync(
        HttpClient client,
        CancellationToken cancellationToken)
    {
        var response = await client.PostAsJsonAsync(
            "/api/v1/passkeys/authentication/options",
            new PasskeyAuthenticationOptionsRequest(),
            cancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<PasskeyAuthenticationOptionsResponse>(
            cancellationToken))!.Options;
    }

    private static JsonElement ParseJwtPayload(string token)
    {
        var encoded = token.Split('.')[1];
        return JsonDocument.Parse(Microsoft.AspNetCore.WebUtilities.WebEncoders.Base64UrlDecode(encoded))
            .RootElement.Clone();
    }
}
