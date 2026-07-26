namespace IntegrationTests.Infrastructure;

/// <summary>
/// Locates the repository root from a test binary's output directory.
/// </summary>
/// <remarks>
/// Tests that read committed files — the documentation guards, the <c>http/</c> sync guard —
/// all need this, and the walk-up convention should have exactly one definition to change if
/// the project file is ever renamed or the layout stops being flat.
/// </remarks>
internal static class RepositoryPaths
{
    private const string ProjectFileName = "dotnet-web-api-startpack.csproj";

    public static string Root { get; } = Locate();

    private static string Locate()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null
               && !File.Exists(Path.Combine(directory.FullName, ProjectFileName)))
        {
            directory = directory.Parent;
        }

        return directory?.FullName
               ?? throw new DirectoryNotFoundException("Could not locate the repository root.");
    }
}
