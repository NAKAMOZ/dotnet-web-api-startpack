using System.Collections.Concurrent;
using System.Net;

namespace Api.Services.Email;

public sealed class EmbeddedEmailTemplateRenderer : IEmailTemplateRenderer
{
    private const string ResourcePrefix = "Api.Templates.";

    /// <summary>
    /// Embedded resources cannot change while the process runs, so each template is read from
    /// the manifest once. Without this every notification re-opens the stream and blocks on a
    /// synchronous read from the request thread.
    /// </summary>
    private static readonly ConcurrentDictionary<string, string> Templates = new(StringComparer.Ordinal);

    public string Render(string templateName, IReadOnlyDictionary<string, string> values)
    {
        var rendered = Templates.GetOrAdd(templateName, Load);

        foreach (var (name, value) in values)
        {
            rendered = rendered.Replace(
                $"{{{{{name}}}}}",
                WebUtility.HtmlEncode(value),
                StringComparison.Ordinal);
        }

        return rendered;
    }

    private static string Load(string templateName)
    {
        var assembly = typeof(EmbeddedEmailTemplateRenderer).Assembly;
        var resourceName = $"{ResourcePrefix}{templateName}.html";

        using var stream = assembly.GetManifestResourceStream(resourceName)
                           ?? throw new InvalidOperationException($"Email template '{templateName}' was not embedded.");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
