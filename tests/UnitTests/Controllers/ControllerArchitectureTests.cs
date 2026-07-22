using System.Reflection;
using Api.Controllers;
using Api.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace UnitTests.Controllers;

/// <summary>
/// Architecture rules for controllers (§11). These are review rules made mechanical — each
/// one describes a mistake that is invisible in a diff and expensive later.
/// </summary>
public class ControllerArchitectureTests
{
    private static readonly Assembly ApiAssembly = typeof(ApiControllerBase).Assembly;

    private static IEnumerable<Type> Controllers =>
        ApiAssembly.GetTypes()
            .Where(type => type is { IsClass: true, IsAbstract: false })
            .Where(type => typeof(ControllerBase).IsAssignableFrom(type));

    private static IEnumerable<MethodInfo> Actions =>
        Controllers.SelectMany(controller =>
            controller.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly));

    [Fact]
    public void EveryInventoryControllerExists()
    {
        string[] expected =
        [
            nameof(AuthController), nameof(SocialAuthController), nameof(SessionsController),
            nameof(EmailVerificationController), nameof(PasswordResetController), nameof(MfaController),
            nameof(PasskeysController), nameof(ApiKeysController), nameof(UsersController),
            nameof(AdminUsersController), nameof(AdminUserRolesController),
            nameof(AdminUserSessionsController), nameof(AdminAuditLogsController),
            nameof(WellKnownController),
        ];

        var present = Controllers.Select(type => type.Name).ToHashSet(StringComparer.Ordinal);

        Assert.All(expected, name => Assert.Contains(name, present));
    }

    [Fact]
    public void NoControllerTouchesTheDbContextDirectly()
    {
        // A controller with a DbContext is a controller with business logic in it — the
        // query is the logic. It also cannot be unit-tested without a database, and it puts
        // the ownership scoping that prevents IDOR into thirteen places instead of one.
        var violations = Controllers
            .SelectMany(controller => controller.GetConstructors())
            .SelectMany(constructor => constructor.GetParameters())
            .Where(parameter => typeof(AppDbContext).IsAssignableFrom(parameter.ParameterType))
            .Select(parameter => $"{parameter.Member.DeclaringType!.Name}({parameter.Name})");

        Assert.Empty(violations);
    }

    [Fact]
    public void EveryActionAcceptsACancellationToken()
    {
        // A dropped token means a client that gave up is still being served: the query runs
        // to completion, the connection stays held, and under load the work outlives every
        // caller waiting for it.
        var violations = Actions
            .Where(action => action.GetParameters().All(p => p.ParameterType != typeof(CancellationToken)))
            .Select(action => $"{action.DeclaringType!.Name}.{action.Name}");

        Assert.Empty(violations);
    }

    [Fact]
    public void EveryActionDeclaresItsResponses()
    {
        // The OpenAPI document (§18) and the endpoint docs (§19) are generated from these.
        // An unannotated action documents itself as returning 200 with an unknown body.
        var violations = Actions
            .Where(action => !action.GetCustomAttributes<ProducesResponseTypeAttribute>().Any())
            .Select(action => $"{action.DeclaringType!.Name}.{action.Name}");

        Assert.Empty(violations);
    }

    [Fact]
    public void EveryActionDeclaresItsAuthorizationPosture()
    {
        // Deny-by-default is not active until §12, so an action carrying neither [Authorize]
        // nor [AllowAnonymous] is anonymous *right now* — silently, and without anyone
        // deciding it. This test is what makes that decision explicit in every case.
        var violations = Actions
            .Where(action => !HasAuthorizationMetadata(action))
            .Select(action => $"{action.DeclaringType!.Name}.{action.Name}");

        Assert.Empty(violations);
    }

    [Fact]
    public void ActionsStayThin()
    {
        // A crude proxy for the thinness rule: an action's IL should be a call and a return.
        // The real rule is enforced by review; this catches the drift that review misses
        // once the file is long enough that nobody reads the whole thing.
        var violations = Actions
            .Select(action => (Action: action, Size: action.GetMethodBody()?.GetILAsByteArray()?.Length ?? 0))
            .Where(candidate => candidate.Size > 400)
            .Select(candidate => $"{candidate.Action.DeclaringType!.Name}.{candidate.Action.Name} ({candidate.Size} bytes IL)");

        Assert.Empty(violations);
    }

    private static bool HasAuthorizationMetadata(MethodInfo action)
    {
        var declaringType = action.DeclaringType!;

        return action.GetCustomAttributes<AuthorizeAttribute>(inherit: true).Any()
               || action.GetCustomAttributes<AllowAnonymousAttribute>(inherit: true).Any()
               || declaringType.GetCustomAttributes<AuthorizeAttribute>(inherit: true).Any()
               || declaringType.GetCustomAttributes<AllowAnonymousAttribute>(inherit: true).Any();
    }
}
