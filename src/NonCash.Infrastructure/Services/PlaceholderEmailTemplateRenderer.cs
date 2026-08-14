using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using NonCash.Core.Interfaces;

namespace NonCash.Infrastructure.Services;

/// <summary>
/// Simple file-based email template renderer. Templates are plain HTML files with
/// {{Placeholder}} markers. The renderer loads the file and replaces all markers.
/// </summary>
public class PlaceholderEmailTemplateRenderer : IEmailTemplateRenderer
{
    private readonly string _templatesDirectory;
    private readonly ILogger<PlaceholderEmailTemplateRenderer> _logger;

    public PlaceholderEmailTemplateRenderer(ILogger<PlaceholderEmailTemplateRenderer> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        // Templates ship alongside the Infrastructure assembly in /EmailTemplates.
        var assemblyLocation = AppContext.BaseDirectory;
        _templatesDirectory = Path.Combine(assemblyLocation, "EmailTemplates");
    }

    public Task<string> RenderAsync(string templateName, Dictionary<string, string?> placeholders, CancellationToken cancellationToken = default)
    {
        var filePath = Path.Combine(_templatesDirectory, $"{templateName}.html");
        string content;

        if (!File.Exists(filePath))
        {
            _logger.LogWarning("Email template '{TemplateName}' not found at {FilePath}. Falling back to plain text.", templateName, filePath);
            // Minimal fallback so the email still has readable content.
            content = string.Join("<br/>", placeholders.Select(p => $"<b>{p.Key}:</b> {p.Value}"));
            return Task.FromResult(content);
        }

        content = File.ReadAllText(filePath);

        // Replace each {{Key}} marker. Missing placeholders are left as-is to aid debugging.
        foreach (var (key, value) in placeholders)
        {
            content = content.Replace($"{{{{{key}}}}}", value ?? string.Empty, StringComparison.OrdinalIgnoreCase);
        }

        return Task.FromResult(content);
    }
}
