using System.Net;

namespace Api.Services.Email;

public sealed class EmbeddedEmailTemplateRenderer : IEmailTemplateRenderer
{
    private const string ResourcePrefix = "Api.Templates.";

    public string Render(string templateName, IReadOnlyDictionary<string, string> values)
    {
        var assembly = typeof(EmbeddedEmailTemplateRenderer).Assembly;
        var resourceName = $"{ResourcePrefix}{templateName}.html";

        using var stream = assembly.GetManifestResourceStream(resourceName)
                           ?? throw new InvalidOperationException($"Email template '{templateName}' was not embedded.");
        using var reader = new StreamReader(stream);
        var rendered = reader.ReadToEnd();

        foreach (var (name, value) in values)
        {
            rendered = rendered.Replace(
                $"{{{{{name}}}}}",
                WebUtility.HtmlEncode(value),
                StringComparison.Ordinal);
        }

        return rendered;
    }
}
