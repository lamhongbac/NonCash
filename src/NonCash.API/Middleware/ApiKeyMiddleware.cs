using Microsoft.EntityFrameworkCore;
using NonCash.Core.Entities;
using NonCash.Infrastructure.Data;

namespace NonCash.API.Middleware;

/// <summary>
/// Validates the X-API-Key header for POS endpoints (route prefix /api/v1/pos).
/// On success, attaches outlet identity to HttpContext.Items["pos.outlet_id"] and "pos.brand_id".
/// </summary>
public class ApiKeyMiddleware
{
    private readonly RequestDelegate _next;
    private const string HeaderName = "X-API-Key";
    private const string PosPathPrefix = "/api/v1/pos";

    public ApiKeyMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context, ApplicationDbContext db)
    {
        var path = context.Request.Path.Value ?? string.Empty;

        // Only enforce on POS endpoints
        if (!path.StartsWith(PosPathPrefix, StringComparison.OrdinalIgnoreCase))
        {
            await _next(context);
            return;
        }

        if (!context.Request.Headers.TryGetValue(HeaderName, out var apiKeyValues)
            || string.IsNullOrWhiteSpace(apiKeyValues.ToString()))
        {
            await WriteUnauthorized(context, "MissingApiKey");
            return;
        }

        var apiKey = apiKeyValues.ToString().Trim();

        // Match the supplied key against an outlet's ApiKeyPrefix
        // (For dev: full key === prefix. Production: hash + lookup.)
        var outlet = await db.Set<Outlet>()
            .AsNoTracking()
            .FirstOrDefaultAsync(o => o.ApiKeyPrefix == apiKey
                                   && o.Status == OutletStatus.Active);

        if (outlet == null)
        {
            await WriteUnauthorized(context, "InvalidApiKey");
            return;
        }

        // Attach outlet claims for downstream handlers
        context.Items["pos.outlet_id"] = outlet.Id;
        context.Items["pos.brand_id"] = outlet.BrandId;

        await _next(context);
    }

    private static async Task WriteUnauthorized(HttpContext context, string reason)
    {
        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
        context.Response.ContentType = "application/json";
        await context.Response.WriteAsync($"{{\"status\":\"Invalid\",\"reason\":\"{reason}\"}}");
    }
}
