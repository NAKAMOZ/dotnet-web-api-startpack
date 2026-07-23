using System.Reflection;
using Api.Exceptions;
using Api.Validators.Common;

namespace UnitTests.Errors;

/// <summary>
/// Keeps the error catalogue (<c>Documentation/Errors.md</c>) honest against the code, and
/// the exception map complete (§13).
/// </summary>
/// <remarks>
/// A catalogue that drifts is worse than none: a client trusts it, branches on a code that
/// no longer exists, and the failure appears in production as an unhandled case.
/// </remarks>
public class ErrorCatalogTests
{
    private static readonly string CatalogPath = Path.Combine(
        RepositoryRoot(), "Documentation", "Errors.md");

    private static readonly string Catalog = File.ReadAllText(CatalogPath);

    [Fact]
    public void EveryDomainExceptionIsMappedToADeliberateStatus()
    {
        // The map's fallback arm turns an unmapped DomainException into a 500. That is the
        // right default — a guessed 400 would hide the gap behind something that looks like
        // the client's fault — but it must never be reached in practice.
        var unmapped = DomainExceptionTypes()
            .Select(CreateInstance)
            .Where(exception => ExceptionToProblemDetailsMap.Map(exception).Status == 500)
            .Select(exception => exception.GetType().Name)
            .ToArray();

        Assert.Empty(unmapped);
    }

    [Fact]
    public void EveryDomainExceptionCodeIsCatalogued()
    {
        // ConflictException is excluded: it carries a per-case code supplied at the throw
        // site rather than a fixed one, so there is no single value to look for. The
        // catalogue documents the family instead. Every other subclass has one code, and
        // that code must appear.
        var missing = DomainExceptionTypes()
            .Where(type => type != typeof(ConflictException))
            .Select(CreateInstance)
            .Select(exception => exception.ErrorCode)
            .Distinct()
            .Where(code => !Catalog.Contains($"`{code}`", StringComparison.Ordinal))
            .ToArray();

        Assert.Empty(missing);
    }

    [Fact]
    public void EveryFrameworkErrorCodeIsCatalogued()
    {
        var missing = ConstantsOf(typeof(ErrorCodes))
            .Where(code => !Catalog.Contains($"`{code}`", StringComparison.Ordinal))
            .ToArray();

        Assert.Empty(missing);
    }

    [Fact]
    public void EveryValidationErrorCodeIsCatalogued()
    {
        var missing = ConstantsOf(typeof(ValidationErrorCodes))
            .Where(code => !Catalog.Contains($"`{code}`", StringComparison.Ordinal))
            .ToArray();

        Assert.Empty(missing);
    }

    [Fact]
    public void LockoutIsIndistinguishableFromABadPassword()
    {
        // The single most important assertion in this file. If AccountLockedException ever
        // escapes with its own status or code, the login endpoint starts telling attackers
        // both that the address exists and that their guessing is working.
        var locked = ExceptionToProblemDetailsMap.Map(new AccountLockedException(DateTimeOffset.UtcNow));
        var wrongPassword = ExceptionToProblemDetailsMap.Map(new InvalidCredentialsException());

        Assert.Equal(wrongPassword, locked);
    }

    [Fact]
    public void ProblemTypeMatchesTheErrorCode()
    {
        var problem = ExceptionToProblemDetailsMap.ToProblemDetails(
            new InvalidCredentialsException(),
            includeDetail: false);

        Assert.Equal("/errors/invalid_credentials", problem.Type);
        Assert.Equal("invalid_credentials", problem.Extensions[ProblemDetailsExtensions.ErrorCode]);
    }

    [Fact]
    public void NonDomainExceptionDetailIsWithheldOutsideDevelopment()
    {
        // The message here is the shape of a real leak: connection strings, file paths and
        // SQL fragments all arrive in exception messages.
        var exception = new InvalidOperationException("Host=db;Password=hunter2");

        var withheld = ExceptionToProblemDetailsMap.ToProblemDetails(exception, includeDetail: false);
        var shown = ExceptionToProblemDetailsMap.ToProblemDetails(exception, includeDetail: true);

        Assert.Null(withheld.Detail);
        Assert.Equal("Host=db;Password=hunter2", shown.Detail);
    }

    [Fact]
    public void DomainExceptionDetailIsAlwaysShown()
    {
        // A DomainException message is written FOR the client — "That email address is
        // already registered." Withholding it would leave a 409 with nothing in it.
        var problem = ExceptionToProblemDetailsMap.ToProblemDetails(
            new ConflictException("mfa_already_enrolled", "MFA is already enabled."),
            includeDetail: false);

        Assert.Equal("MFA is already enabled.", problem.Detail);
    }

    private static IEnumerable<Type> DomainExceptionTypes() =>
        typeof(DomainException).Assembly.GetTypes()
            .Where(type => type is { IsClass: true, IsAbstract: false })
            .Where(typeof(DomainException).IsAssignableFrom);

    private static DomainException CreateInstance(Type type)
    {
        var constructor = type.GetConstructors().MinBy(candidate => candidate.GetParameters().Length)!;

        var arguments = constructor.GetParameters()
            .Select(parameter => parameter.ParameterType switch
            {
                var t when t == typeof(string) => (object)"test",
                var t when t == typeof(Guid) => Guid.Empty,
                var t when t == typeof(DateTimeOffset) => DateTimeOffset.UnixEpoch,
                _ => throw new InvalidOperationException($"No test value for {parameter.ParameterType}."),
            })
            .ToArray();

        return (DomainException)constructor.Invoke(arguments);
    }

    private static IEnumerable<string> ConstantsOf(Type type) =>
        type.GetFields(BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy)
            .Where(field => field is { IsLiteral: true, IsInitOnly: false })
            .Select(field => (string)field.GetRawConstantValue()!);

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
