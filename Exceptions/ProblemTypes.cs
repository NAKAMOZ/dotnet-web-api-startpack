namespace Api.Exceptions;

/// <summary>
/// Builds the RFC 9457 <c>type</c> URI for an error code.
/// </summary>
/// <remarks>
/// <c>/errors/&lt;code&gt;</c>, resolved against the request's base URI. Relative rather than
/// absolute because the alternative — baking a hostname into every error response — is
/// wrong in every environment but the one it was written for, and behind a reverse proxy it
/// is wrong everywhere.
/// <para>
/// The URI is a stable identifier, not necessarily a live page. §19 publishes the catalogue
/// at these paths; until then it identifies an entry in <c>Documentation/Errors.md</c>.
/// </para>
/// </remarks>
public static class ProblemTypes
{
    private const string Prefix = "/errors/";

    /// <summary>The <c>type</c> URI for a given error code.</summary>
    public static string For(string errorCode) => Prefix + errorCode;
}
