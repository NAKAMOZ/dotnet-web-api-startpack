using System.Net.Http.Headers;
using System.Text.Json.Serialization;
using Api.Configuration;
using Api.Data;
using Api.Data.Seeding;
using Api.DTOs.Auth;
using Api.DTOs.SocialAuth;
using Api.Exceptions;
using Api.Models;
using Api.Models.Enums;
using Api.Services.Audit;
using Api.Services.Auth;
using Api.Services.Crypto;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Api.Services.SocialAuth;

public sealed class SocialAuthService(
    AppDbContext dbContext,
    ITokenGenerator tokenGenerator,
    IAuthenticationSessionFactory sessionFactory,
    IHttpClientFactory httpClientFactory,
    IHttpContextAccessor httpContextAccessor,
    IOptions<SocialProviderOptions> providerOptions,
    IAuditLogger auditLogger,
    TimeProvider timeProvider) : ISocialAuthService
{
    private static readonly TimeSpan StateLifetime = TimeSpan.FromMinutes(10);
    private readonly SocialProviderOptions _providers = providerOptions.Value;

    public async Task<SocialAuthorizeResponse> AuthorizeAsync(
        string provider,
        CancellationToken cancellationToken)
    {
        var normalized = NormalizeProvider(provider);
        var configuration = Provider(normalized);
        var state = tokenGenerator.NewOpaqueToken();
        var expiresAt = timeProvider.GetUtcNow() + StateLifetime;
        dbContext.VerificationTokens.Add(new VerificationToken
        {
            Type = VerificationTokenType.SocialAuthorizationState,
            TokenHash = tokenGenerator.Hash(state),
            ExpiresAt = expiresAt,
        });
        await dbContext.SaveChangesAsync(cancellationToken);

        var callback = CallbackUri(normalized);
        var authorizationUrl = normalized == "google"
            ? "https://accounts.google.com/o/oauth2/v2/auth"
              + $"?client_id={Uri.EscapeDataString(configuration.ClientId!)}"
              + $"&redirect_uri={Uri.EscapeDataString(callback)}"
              + "&response_type=code&scope=openid%20email%20profile"
              + $"&state={Uri.EscapeDataString(state)}"
            : "https://github.com/login/oauth/authorize"
              + $"?client_id={Uri.EscapeDataString(configuration.ClientId!)}"
              + $"&redirect_uri={Uri.EscapeDataString(callback)}"
              + "&scope=read%3Auser%20user%3Aemail"
              + $"&state={Uri.EscapeDataString(state)}";

        return new SocialAuthorizeResponse { AuthorizationUrl = authorizationUrl, ExpiresAt = expiresAt };
    }

    public async Task<LoginResponse> CallbackAsync(
        string provider,
        SocialCallbackQuery query,
        CancellationToken cancellationToken)
    {
        var normalized = NormalizeProvider(provider);

        if (!string.IsNullOrWhiteSpace(query.Error)
            || string.IsNullOrWhiteSpace(query.Code)
            || string.IsNullOrWhiteSpace(query.State))
        {
            throw new InvalidTokenException();
        }

        await ConsumeStateAsync(query.State, cancellationToken);
        var identity = normalized == "google"
            ? await GetGoogleIdentityAsync(query.Code, cancellationToken)
            : await GetGitHubIdentityAsync(query.Code, cancellationToken);
        var account = await dbContext.Accounts
            .Include(candidate => candidate.User)
            .SingleOrDefaultAsync(
                candidate => candidate.Provider == normalized
                             && candidate.ProviderAccountId == identity.Subject,
                cancellationToken);
        var createdUser = false;
        var user = account?.User;

        if (user is null)
        {
            var email = identity.Email;
            var canUseProviderEmail = !string.IsNullOrWhiteSpace(email)
                                      && !await dbContext.Users.AnyAsync(
                                          candidate => candidate.Email == email,
                                          cancellationToken);

            if (!canUseProviderEmail)
            {
                // ADR-0019 forbids email-only account linking. A provider subject with an
                // address already used locally gets a non-routable identity until the user
                // deliberately links accounts through a separately verified flow.
                email = $"{normalized}-{identity.Subject}@invalid.local";
            }

            user = new User
            {
                Email = email!,
                EmailVerified = canUseProviderEmail && identity.EmailVerified,
                DisplayName = identity.DisplayName,
            };
            dbContext.Users.Add(user);
            dbContext.UserRoles.Add(new UserRole { UserId = user.Id, RoleId = RoleSeed.UserRoleId });
            createdUser = true;
            dbContext.Accounts.Add(new Account
            {
                UserId = user.Id,
                Provider = normalized,
                ProviderAccountId = identity.Subject,
            });
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        if (createdUser)
        {
            await auditLogger.LogAsync(
                AuditEventType.UserRegistered,
                user.Id,
                new { Provider = normalized },
                cancellationToken);
        }

        var method = normalized == "google"
            ? AuthenticationMethod.Google
            : AuthenticationMethod.GitHub;
        var response = await sessionFactory.CreateAsync(user.Id, [method], cancellationToken);
        await auditLogger.LogAsync(
            AuditEventType.LoginSucceeded,
            user.Id,
            new { Provider = normalized },
            cancellationToken);
        return response;
    }

    private async Task ConsumeStateAsync(string state, CancellationToken cancellationToken)
    {
        var hash = tokenGenerator.Hash(state);
        var now = timeProvider.GetUtcNow();
        var consumed = await dbContext.VerificationTokens
            .Where(token => token.TokenHash == hash
                            && token.Type == VerificationTokenType.SocialAuthorizationState
                            && token.ConsumedAt == null
                            && token.ExpiresAt > now)
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(token => token.ConsumedAt, now),
                cancellationToken);

        if (consumed == 0)
        {
            throw new InvalidTokenException();
        }
    }

    private async Task<SocialIdentity> GetGoogleIdentityAsync(
        string code,
        CancellationToken cancellationToken)
    {
        var provider = Provider("google");
        var client = httpClientFactory.CreateClient();
        var tokenResponse = await client.PostAsync(
            "https://oauth2.googleapis.com/token",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["client_id"] = provider.ClientId!,
                ["client_secret"] = provider.ClientSecret!,
                ["code"] = code,
                ["grant_type"] = "authorization_code",
                ["redirect_uri"] = CallbackUri("google"),
            }),
            cancellationToken);
        tokenResponse.EnsureSuccessStatusCode();
        var token = await tokenResponse.Content.ReadFromJsonAsync<OAuthTokenResponse>(
                        cancellationToken: cancellationToken)
                    ?? throw new InvalidTokenException();
        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            "https://openidconnect.googleapis.com/v1/userinfo");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token.AccessToken);
        var response = await client.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
        var profile = await response.Content.ReadFromJsonAsync<GoogleProfile>(
                          cancellationToken: cancellationToken)
                      ?? throw new InvalidTokenException();
        return new SocialIdentity(profile.Subject, profile.Email, profile.EmailVerified, profile.Name);
    }

    private async Task<SocialIdentity> GetGitHubIdentityAsync(
        string code,
        CancellationToken cancellationToken)
    {
        var provider = Provider("github");
        var client = httpClientFactory.CreateClient();
        using var tokenRequest = new HttpRequestMessage(HttpMethod.Post, "https://github.com/login/oauth/access_token")
        {
            Content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["client_id"] = provider.ClientId!,
                ["client_secret"] = provider.ClientSecret!,
                ["code"] = code,
                ["redirect_uri"] = CallbackUri("github"),
            }),
        };
        tokenRequest.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        var tokenResponse = await client.SendAsync(tokenRequest, cancellationToken);
        tokenResponse.EnsureSuccessStatusCode();
        var token = await tokenResponse.Content.ReadFromJsonAsync<OAuthTokenResponse>(
                        cancellationToken: cancellationToken)
                    ?? throw new InvalidTokenException();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token.AccessToken);
        client.DefaultRequestHeaders.UserAgent.ParseAdd("dotnet-web-api-startpack");
        var profile = await client.GetFromJsonAsync<GitHubProfile>(
                          "https://api.github.com/user",
                          cancellationToken)
                      ?? throw new InvalidTokenException();
        var emails = await client.GetFromJsonAsync<List<GitHubEmail>>(
                         "https://api.github.com/user/emails",
                         cancellationToken)
                     ?? [];
        var email = emails.FirstOrDefault(candidate => candidate.Primary && candidate.Verified);
        return new SocialIdentity(
            profile.Id.ToString(System.Globalization.CultureInfo.InvariantCulture),
            email?.Email,
            email is not null,
            profile.Name ?? profile.Login);
    }

    private string CallbackUri(string provider)
    {
        var request = httpContextAccessor.HttpContext?.Request
                      ?? throw new InvalidOperationException("Social auth requires an HTTP request.");
        return $"{request.Scheme}://{request.Host}{request.PathBase}/api/v1/auth/social/{provider}/callback";
    }

    private SocialProviderOptions.Provider Provider(string provider)
    {
        var selected = provider == "google" ? _providers.Google : _providers.GitHub;

        if (!selected.Enabled
            || string.IsNullOrWhiteSpace(selected.ClientId)
            || string.IsNullOrWhiteSpace(selected.ClientSecret))
        {
            throw new UnsupportedProviderException();
        }

        return selected;
    }

    private static string NormalizeProvider(string provider) =>
        provider.Trim().ToLowerInvariant() is var normalized
        && normalized is "google" or "github"
            ? normalized
            : throw new UnsupportedProviderException();

    private sealed record SocialIdentity(
        string Subject,
        string? Email,
        bool EmailVerified,
        string? DisplayName);

    private sealed record OAuthTokenResponse(
        [property: JsonPropertyName("access_token")] string AccessToken);

    private sealed record GoogleProfile(
        [property: JsonPropertyName("sub")] string Subject,
        [property: JsonPropertyName("email")] string Email,
        [property: JsonPropertyName("email_verified")] bool EmailVerified,
        [property: JsonPropertyName("name")] string? Name);

    private sealed record GitHubProfile(
        [property: JsonPropertyName("id")] long Id,
        [property: JsonPropertyName("login")] string Login,
        [property: JsonPropertyName("name")] string? Name);

    private sealed record GitHubEmail(
        [property: JsonPropertyName("email")] string Email,
        [property: JsonPropertyName("primary")] bool Primary,
        [property: JsonPropertyName("verified")] bool Verified);
}
