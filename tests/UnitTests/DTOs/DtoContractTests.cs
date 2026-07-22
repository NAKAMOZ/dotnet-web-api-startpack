using System.Reflection;
using Api.Data;
using Api.Models;

namespace UnitTests.DTOs;

/// <summary>
/// Reflection guards over the whole <c>DTOs/</c> tree (§9).
/// </summary>
/// <remarks>
/// These exist because the failure they catch is invisible in review of the change that
/// causes it. Nobody adds <c>PasswordHash</c> to a response DTO on purpose — it arrives by
/// returning an entity "just for now", or by a mapper copying every property. Both look
/// like small conveniences in a diff and both publish a credential.
/// </remarks>
public class DtoContractTests
{
    private static readonly Assembly ApiAssembly = typeof(AppDbContext).Assembly;

    private static IEnumerable<Type> DtoTypes =>
        ApiAssembly.GetTypes()
            .Where(type => type.Namespace?.StartsWith("Api.DTOs", StringComparison.Ordinal) == true)
            .Where(type => type is { IsClass: true, IsAbstract: false });

    /// <summary>
    /// Property names that must never appear on a DTO, whatever the shape around them. The
    /// list is by name rather than by type because these are all strings — the type system
    /// cannot tell a hash from a display name, so the naming convention is the guard.
    /// </summary>
    private static readonly string[] ForbiddenNames =
    [
        "PasswordHash",
        "TokenHash",
        "CodeHash",
        "KeyHash",
        "SecretEncrypted",
        "PrivateKeyProtected",
        "SecurityStamp",
    ];

    [Fact]
    public void NoDtoExposesAStoredSecret()
    {
        var violations =
            from type in DtoTypes
            from property in type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
            where ForbiddenNames.Contains(property.Name, StringComparer.OrdinalIgnoreCase)
            select $"{type.Name}.{property.Name}";

        Assert.Empty(violations);
    }

    [Fact]
    public void NoDtoReferencesAnEntity()
    {
        // Entities are never serialized (§9). A DTO holding one drags the whole object graph
        // — and every hash on it — into a response the moment someone serializes it, and
        // couples the wire contract to the schema so a migration becomes a breaking change.
        var entityNamespace = typeof(User).Namespace!;

        var violations =
            from type in DtoTypes
            from property in type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
            let propertyType = UnwrapCollection(property.PropertyType)
            where propertyType.Namespace == entityNamespace
            select $"{type.Name}.{property.Name} → {propertyType.Name}";

        Assert.Empty(violations);
    }

    [Fact]
    public void EveryDtoIsARecord()
    {
        // Records give value equality and a compiler-generated ToString, both of which make
        // request/response types comparable in tests without hand-written boilerplate.
        var violations = DtoTypes
            .Where(type => type.GetMethod("<Clone>$", BindingFlags.Public | BindingFlags.Instance) is null)
            .Select(type => type.FullName!);

        Assert.Empty(violations);
    }

    [Fact]
    public void RequestAndResponseTypesAreNeverShared()
    {
        // A type used in both directions eventually grows a property that is only valid one
        // way — server-assigned ids a client must not send, secrets a client must not
        // receive — and there is then no place to say so.
        var suspicious = DtoTypes
            .Where(type => type.Name.EndsWith("Request", StringComparison.Ordinal)
                           && type.Name.EndsWith("Response", StringComparison.Ordinal))
            .Select(type => type.FullName!);

        Assert.Empty(suspicious);
    }

    [Fact]
    public void EveryEndpointFeatureHasDtos()
    {
        // Guards against a feature directory being silently dropped: each namespace below
        // maps to a controller in the §11 inventory.
        string[] expected =
        [
            "Api.DTOs.Auth",
            "Api.DTOs.SocialAuth",
            "Api.DTOs.Sessions",
            "Api.DTOs.EmailVerification",
            "Api.DTOs.PasswordReset",
            "Api.DTOs.Mfa",
            "Api.DTOs.Passkeys",
            "Api.DTOs.ApiKeys",
            "Api.DTOs.Users",
            "Api.DTOs.Admin",
            "Api.DTOs.WellKnown",
            "Api.DTOs.Common",
        ];

        var present = DtoTypes.Select(type => type.Namespace!).Distinct().ToHashSet(StringComparer.Ordinal);

        Assert.All(expected, ns => Assert.Contains(ns, present));
    }

    private static Type UnwrapCollection(Type type)
    {
        if (type.IsArray)
        {
            return type.GetElementType()!;
        }

        return type.IsGenericType ? type.GetGenericArguments()[0] : type;
    }
}
