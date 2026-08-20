using System.Net.Http.Headers;

namespace NonCash.Web.Services;

/// <summary>
/// Delegating handler that attaches the JWT token to outgoing requests and redirects
/// to the login page when the API returns 401 Unauthorized.
/// </summary>
public class AuthHttpHandler : DelegatingHandler
{
    private readonly IServiceProvider _serviceProvider;

    public AuthHttpHandler(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        // Create a scope so we can resolve scoped services inside this singleton-compatible handler.
        await using var scope = _serviceProvider.CreateAsyncScope();
        var authState = scope.ServiceProvider.GetRequiredService<ClientAuthService>();

        var token = await authState.GetTokenAsync();
        if (!string.IsNullOrWhiteSpace(token))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        var response = await base.SendAsync(request, cancellationToken);

        if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
        {
            await authState.LogoutAsync();
        }

        return response;
    }
}
