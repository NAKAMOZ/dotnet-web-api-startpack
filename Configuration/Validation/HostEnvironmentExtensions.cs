namespace Api.Configuration.Validation;

internal static class HostEnvironmentExtensions
{
    /// <summary>
    /// Whether the hardened settings are mandatory rather than merely recommended.
    /// </summary>
    /// <remarks>
    /// Development and Testing are the only environments allowed to relax them: the first
    /// runs on plain HTTP against localhost, the second on an in-memory TestServer that never
    /// sees a proxy. Every other environment — Staging included — is treated as production.
    /// <para>
    /// Written once because more than one validator asks the question, and a second spelling
    /// of it would be a second place to add the next exempt environment to.
    /// </para>
    /// </remarks>
    public static bool IsProductionLike(this IHostEnvironment environment) =>
        !environment.IsDevelopment() && !environment.IsEnvironment("Testing");
}
