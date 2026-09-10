namespace NonCash.Web.Services;

/// <summary>
/// Delegating handler that turns transport failures into HTTP status codes so pages can show
/// a readable error instead of an exception. It deliberately does not attach the JWT: the
/// handler chain is built from a root scope, so the <see cref="ClientAuthService"/> resolved
/// here is not the Blazor circuit's instance and has no token. Pages must add the
/// Authorization header themselves.
/// </summary>
public class AuthHttpHandler : DelegatingHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        try
        {
            return await base.SendAsync(request, cancellationToken);
        }
        catch (HttpRequestException)
        {
            // API is unreachable — return 503 so calling pages can show a friendly error.
            return new HttpResponseMessage(System.Net.HttpStatusCode.ServiceUnavailable);
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            // Request timed out — return 504 Gateway Timeout.
            return new HttpResponseMessage(System.Net.HttpStatusCode.GatewayTimeout);
        }
    }
}
