using System.Reflection;

namespace Api.Validators.Common;

/// <summary>
/// The deny list of predictable passwords, loaded once from the embedded
/// <c>CommonPasswords.txt</c>.
/// </summary>
/// <remarks>
/// Embedded rather than read from disk: a rule whose data file can be missing at deploy
/// time fails open, and a password policy that fails open is not a policy. Loading throws
/// if the resource is absent, so the failure is a boot error rather than a silently
/// permissive validator.
/// </remarks>
public static class PasswordDenyList
{
    /// <summary>
    /// Matched by suffix rather than by full name. The generated resource name is
    /// <c>&lt;RootNamespace&gt;.&lt;path&gt;</c> — <c>Api.Validators.Common.CommonPasswords.txt</c>
    /// today — so hardcoding it couples a security control to a csproj property that has
    /// already changed once in this project's history.
    /// </summary>
    private const string ResourceSuffix = "CommonPasswords.txt";

    private static readonly Lazy<FrozenSetOfPasswords> Entries = new(Load, LazyThreadSafetyMode.ExecutionAndPublication);

    /// <summary>Number of loaded entries. Exposed so a test can assert the resource actually loaded.</summary>
    public static int Count => Entries.Value.Values.Count;

    /// <summary>
    /// Whether the password is on the list. Case-insensitive — an attacker's guess list is
    /// not case-sensitive either, so <c>Password123!</c> and <c>password123!</c> are the
    /// same guess.
    /// </summary>
    public static bool Contains(string password) => Entries.Value.Values.Contains(password);

    private static FrozenSetOfPasswords Load()
    {
        var assembly = Assembly.GetExecutingAssembly();

        var resourceName = assembly.GetManifestResourceNames()
            .SingleOrDefault(name => name.EndsWith(ResourceSuffix, StringComparison.Ordinal))
            ?? throw new InvalidOperationException(
                $"No embedded resource ending in '{ResourceSuffix}' was found. The password deny " +
                "list is a security control — failing loudly rather than validating passwords without it.");

        using var stream = assembly.GetManifestResourceStream(resourceName)!;

        using var reader = new StreamReader(stream);

        var values = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        while (reader.ReadLine() is { } line)
        {
            var entry = line.Trim();

            if (entry.Length == 0 || entry.StartsWith('#'))
            {
                continue;
            }

            values.Add(entry);
        }

        return new FrozenSetOfPasswords(values);
    }

    private sealed record FrozenSetOfPasswords(HashSet<string> Values);
}
