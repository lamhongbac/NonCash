using NonCash.Core.Interfaces;

namespace NonCash.API.Middleware;

/// <summary>
/// Validates the X-API-Key header for Integration endpoints (route prefix /integration).
/// On success, attaches partner identity and authorized brand IDs to HttpContext.Items.
/// </summary>
public class IntegrationApiKeyMiddleware
{
    private readonly RequestDelegate _next;
    private const string HeaderName = "X-API-Key";
    private const string IntegrationPathPrefix = "/integration";

    public IntegrationApiKeyMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context, IIntegrationPartnerService partnerService)
    {
        var path = context.Request.Path.Value ?? string.Empty;

        // Only enforce on integration endpoints
        if (!path.StartsWith(IntegrationPathPrefix, StringComparison.OrdinalIgnoreCase))
        {
            await _next(context);
            return;
        }

        if (!context.Request.Headers.TryGetValue(HeaderName, out var apiKeyValues)
            || string.IsNullOrWhiteSpace(apiKeyValues.ToString()))
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsync("{\"error\":\"Missing X-API-Key header.\"}");
            return;
        }

        var apiKey = apiKeyValues.ToString().Trim();
        var (partner, brandIds) = await partnerService.ValidateApiKeyAsync(apiKey);

        if (partner == null)
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsync("{\"error\":\"Invalid or inactive API key.\"}");
            return;
        }

        // Attach partner context for downstream controllers
        context.Items["integration.partner_id"] = partner.Id;
        context.Items["integration.brand_ids"] = brandIds;
        context.Items["integration.webhook_secret"] = partner.WebhookSecret;

        await _next(context);
    }
}
