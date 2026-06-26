using System.Security.Claims;

namespace NonCash.API.Middleware;

public class BrandScopeMiddleware
{
    private readonly RequestDelegate _next;

    public BrandScopeMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        if (context.User.Identity?.IsAuthenticated == true)
        {
            var roleClaim = context.User.FindFirst(ClaimTypes.Role)?.Value;
            var brandIdClaim = context.User.FindFirst("brand_id")?.Value;

            // Admin users can operate across brands.
            // Members do not need a brand assignment.
            // Other non-admin users must have a brand_id in their token.
            if (!string.IsNullOrEmpty(roleClaim)
                && roleClaim != "Admin"
                && roleClaim != "Member"
                && string.IsNullOrEmpty(brandIdClaim))
            {
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                await context.Response.WriteAsync("Non-admin users must be assigned to a brand.");
                return;
            }
        }

        await _next(context);
    }
}
