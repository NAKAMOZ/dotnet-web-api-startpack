namespace Api.Configuration;

/// <summary>OAuth client configuration for the two supported social providers.</summary>
public sealed class SocialProviderOptions
{
    public const string SectionName = "SocialProviders";

    /// <summary>
    /// Uses deterministic local identities instead of making OAuth HTTP calls.
    /// Honoured only while the host environment is Development.
    /// </summary>
    public bool DemoMode { get; init; }

    public Provider Google { get; init; } = new();

    public Provider GitHub { get; init; } = new();

    public sealed class Provider
    {
        public bool Enabled { get; init; }

        public string? ClientId { get; init; }

        /// <summary>Secret. Supply through user-secrets or an environment variable.</summary>
        public string? ClientSecret { get; init; }
    }
}
