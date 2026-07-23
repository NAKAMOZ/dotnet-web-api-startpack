using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using Serilog.Core;
using Serilog.Events;

namespace Api.Logging;

/// <summary>
/// Redacts credential-shaped properties out of any object of ours that reaches a log sink by
/// destructuring (§15, ADR-0010's never-logged list).
/// </summary>
/// <remarks>
/// <b>What this does and does not cover.</b> It intercepts <c>{@Thing}</c> — the operator
/// that walks an object's properties — which is how a whole entity or request DTO ends up in
/// a log line, and the only way that happens without the author naming the field. It cannot
/// help with <c>logger.LogInformation("token {Token}", token)</c>: a scalar passed under a
/// name of the author's choosing is indistinguishable from any other string. That half is
/// review discipline plus §22's log-capture test, and it is worth knowing which half is
/// mechanical.
/// <para>
/// <b>Name matching, not attributes.</b> A <c>[Sensitive]</c> attribute would be precise, and
/// would protect exactly the properties somebody remembered to mark. The failure mode here is
/// a new property called <c>ResetTokenHash</c> that nobody marked, so the rule keys off the
/// vocabulary the codebase already uses for secrets — see <see cref="SensitiveFieldNames"/>.
/// </para>
/// <para>
/// Applies only to types from this assembly. Framework and BCL types keep Serilog's default
/// handling; re-implementing destructuring for every type in the process, to catch a
/// <c>Dictionary&lt;string, string&gt;</c> that might hold a token, would cost more than it
/// buys.
/// </para>
/// </remarks>
public sealed class SensitiveDataDestructuringPolicy : IDestructuringPolicy
{
    /// <summary>
    /// Reflection is not cheap enough to repeat per log event, and a log call inside a hot
    /// path is exactly where it would be repeated.
    /// </summary>
    private static readonly ConcurrentDictionary<Type, PropertyInfo[]> PropertyCache = new();

    private static readonly Assembly OwnAssembly = typeof(SensitiveDataDestructuringPolicy).Assembly;

    public bool TryDestructure(
        object value,
        ILogEventPropertyValueFactory propertyValueFactory,
        [NotNullWhen(true)] out LogEventPropertyValue? result)
    {
        var type = value.GetType();

        if (type.Assembly != OwnAssembly || type.IsEnum)
        {
            result = null;
            return false;
        }

        var properties = PropertyCache.GetOrAdd(type, static candidate => candidate
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(property => property.CanRead && property.GetIndexParameters().Length == 0)
            .ToArray());

        if (properties.Length == 0)
        {
            result = null;
            return false;
        }

        var logEventProperties = properties
            .Select(property => new LogEventProperty(
                property.Name,
                DestructureProperty(property, value, propertyValueFactory)))
            .ToList();

        result = new StructureValue(logEventProperties, type.Name);
        return true;
    }

    private static LogEventPropertyValue DestructureProperty(
        PropertyInfo property,
        object instance,
        ILogEventPropertyValueFactory propertyValueFactory)
    {
        if (SensitiveFieldNames.IsSecret(property.Name))
        {
            // The getter is never called. Reading a decrypted TOTP secret only to throw the
            // value away still puts it on the heap, next to whatever produces the crash dump.
            return new ScalarValue(SensitiveFieldNames.RedactedValue);
        }

        object? propertyValue;

        try
        {
            propertyValue = property.GetValue(instance);
        }
        catch (Exception exception) when (exception is TargetInvocationException or NotSupportedException)
        {
            // A computed property that throws must not turn a log call into a failed request.
            return new ScalarValue($"[unreadable: {exception.GetType().Name}]");
        }

        if (propertyValue is string text && SensitiveFieldNames.IsEmail(property.Name))
        {
            return new ScalarValue(SensitiveFieldNames.MaskEmail(text));
        }

        // destructureObjects: true, so nested objects of ours come back through this policy.
        return propertyValueFactory.CreatePropertyValue(propertyValue, destructureObjects: true);
    }
}
