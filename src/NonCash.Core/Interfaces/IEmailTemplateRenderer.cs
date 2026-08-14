namespace NonCash.Core.Interfaces;

/// <summary>
/// Renders an email template by replacing placeholders such as {{RecipientName}}.
/// Implementations may use Razor, Liquid, or simple string replacement.
/// </summary>
public interface IEmailTemplateRenderer
{
    /// <summary>
    /// Renders the named template with the supplied placeholder values.
    /// </summary>
    /// <param name="templateName">Template file name without extension (e.g. "AdminNewRegistration").</param>
    /// <param name="placeholders">Key/value pairs to replace in the template.</param>
    Task<string> RenderAsync(string templateName, Dictionary<string, string?> placeholders, CancellationToken cancellationToken = default);
}
