using System.Text.RegularExpressions;
using Api.Models.Enums;

namespace UnitTests.Audit;

/// <summary>
/// Keeps the event catalog in <c>Documentation/Architecture/AuditTrail.md</c> honest against
/// <see cref="AuditEventType"/>, in both directions (§15).
/// </summary>
/// <remarks>
/// The same reasoning as <c>ErrorCatalogTests</c>. An audit catalog that drifts is worse than
/// none: an administrator reads the document, queries for an event that no longer fires, gets
/// an empty page, and concludes the thing never happened.
/// </remarks>
public class AuditCatalogTests
{
    private static readonly string CatalogSection = ExtractCatalogSection();

    [Fact]
    public void EveryCatalogEventIsDocumented()
    {
        var missing = Enum.GetNames<AuditEventType>()
            .Where(name => !CatalogSection.Contains($"`{ToSnakeCase(name)}`", StringComparison.Ordinal))
            .ToArray();

        Assert.Empty(missing);
    }

    [Fact]
    public void EveryDocumentedEventExistsInTheEnum()
    {
        // The direction that catches a deleted enum member. Without it the document keeps
        // advertising an event the code can no longer produce, and the first test above still
        // passes because it only walks the enum.
        var declared = Enum.GetNames<AuditEventType>()
            .Select(ToSnakeCase)
            .ToHashSet(StringComparer.Ordinal);

        var orphaned = DocumentedEvents()
            .Where(name => !declared.Contains(name))
            .ToArray();

        Assert.Empty(orphaned);
    }

    [Fact]
    public void EveryCatalogEventNamesItsWriter()
    {
        // A row with no writer column filled in is an event nobody produces. §12 has yet to
        // write most of the call sites, so the column names the workstream that will — but it
        // is never blank, because "who writes this" is the question the catalog is for.
        var rows = CatalogSection
            .Split('\n')
            .Where(line => Regex.IsMatch(line, @"^\| `[a-z][a-z_]*` \|"))
            .ToArray();

        Assert.Equal(Enum.GetValues<AuditEventType>().Length, rows.Length);
        Assert.All(rows, row => Assert.Equal(5, row.Split('|').Length - 1));
    }

    /// <summary>
    /// <c>ApiKeyCreated</c> → <c>api_key_created</c>. The stored form is the member name; this
    /// is the prose form the document and the roadmap use.
    /// </summary>
    private static string ToSnakeCase(string memberName) =>
        Regex.Replace(memberName, "(?<!^)([A-Z])", "_$1").ToLowerInvariant();

    private static IEnumerable<string> DocumentedEvents() =>
        Regex.Matches(CatalogSection, @"^\| `([a-z][a-z_]*)` \|", RegexOptions.Multiline)
            .Select(match => match.Groups[1].Value);

    /// <summary>
    /// Only the catalog table is searched, not the whole document. Section 7 documents query
    /// parameters in a table of the same shape, and <c>sort</c> would read as an event name.
    /// </summary>
    private static string ExtractCatalogSection()
    {
        var document = File.ReadAllText(Path.Combine(
            RepositoryRoot(), "Documentation", "Architecture", "AuditTrail.md"));

        var start = document.IndexOf("## 2. The event catalog", StringComparison.Ordinal);
        var end = document.IndexOf("## 3.", StringComparison.Ordinal);

        Assert.True(start >= 0 && end > start, "AuditTrail.md no longer contains a section 2 catalog table.");

        return document[start..end];
    }

    private static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "CLAUDE.md")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName
               ?? throw new InvalidOperationException("Could not locate the repository root.");
    }
}
