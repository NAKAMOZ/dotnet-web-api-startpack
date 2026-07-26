using System.ComponentModel.DataAnnotations;

namespace Api.Configuration;

/// <summary>SMTP delivery settings. Credentials are supplied only through a secret channel.</summary>
public sealed class EmailOptions
{
    public const string SectionName = "Email";

    [Required(AllowEmptyStrings = false)]
    public string Host { get; init; } = "localhost";

    [Range(1, 65_535)]
    public int Port { get; init; } = 1025;

    [Required(AllowEmptyStrings = false)]
    [EmailAddress]
    public string FromAddress { get; init; } = "auth@localhost.dev";

    public bool UseTls { get; init; }

    public string? Username { get; init; }

    /// <summary>Secret. Never put this value in an appsettings file.</summary>
    public string? Password { get; init; }
}
