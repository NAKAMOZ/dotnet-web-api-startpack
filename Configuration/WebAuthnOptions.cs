using System.ComponentModel.DataAnnotations;

namespace Api.Configuration;

/// <summary>WebAuthn relying-party identity and accepted browser origins.</summary>
public sealed class WebAuthnOptions
{
    public const string SectionName = "WebAuthn";

    /// <summary>RP ID sent to authenticators; a DNS host name without scheme or port.</summary>
    [Required(AllowEmptyStrings = false)]
    [StringLength(253, MinimumLength = 1)]
    public string ServerDomain { get; init; } = "localhost";

    /// <summary>Human-readable relying-party name shown by authenticators.</summary>
    [Required(AllowEmptyStrings = false)]
    [StringLength(100, MinimumLength = 1)]
    public string ServerName { get; init; } = "dotnet-web-api-startpack";

    /// <summary>Exact browser origins accepted in client data.</summary>
    [MinLength(1)]
    public string[] Origins { get; set; } =
    [
        "https://localhost:7052",
        "http://localhost:5035",
    ];
}
