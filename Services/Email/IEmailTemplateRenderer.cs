namespace Api.Services.Email;

public interface IEmailTemplateRenderer
{
    string Render(string templateName, IReadOnlyDictionary<string, string> values);
}
