using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace NonCash.Web.Services;

public class ClientAuthService
{
    private readonly IJSRuntime _jsRuntime;
    private readonly NavigationManager _navigation;
    private readonly IConfiguration _configuration;

    public event Action? OnAuthStateChanged;

    public string? Token { get; private set; }
    public string? FullName { get; private set; }
    public string? Role { get; private set; }
    public Guid? BrandId { get; private set; }
    public Guid? UserId { get; private set; }
    public Guid? CustomerId { get; private set; }
    public DateTime? TokenExpiry { get; private set; }
    public bool IsLoggedIn => !string.IsNullOrEmpty(Token) && !IsTokenExpired();
    public bool IsMember => Role?.Equals("Member", StringComparison.OrdinalIgnoreCase) == true;

    public int IdleTimeoutMinutes => _configuration.GetSection("Auth").GetValue<int?>("IdleTimeoutMinutes") ?? 30;

    public ClientAuthService(IJSRuntime jsRuntime, NavigationManager navigation, IConfiguration configuration)
    {
        _jsRuntime = jsRuntime;
        _navigation = navigation;
        _configuration = configuration;
    }

    public async Task InitializeAsync()
    {
        try
        {
            Token = await _jsRuntime.InvokeAsync<string?>("localStorage.getItem", "authToken");
            FullName = await _jsRuntime.InvokeAsync<string?>("localStorage.getItem", "authFullName");
            Role = await _jsRuntime.InvokeAsync<string?>("localStorage.getItem", "authRole");

            var brandIdStr = await _jsRuntime.InvokeAsync<string?>("localStorage.getItem", "authBrandId");
            if (Guid.TryParse(brandIdStr, out var bid))
                BrandId = bid;

            var userIdStr = await _jsRuntime.InvokeAsync<string?>("localStorage.getItem", "authUserId");
            if (Guid.TryParse(userIdStr, out var uid))
                UserId = uid;

            var customerIdStr = await _jsRuntime.InvokeAsync<string?>("localStorage.getItem", "authCustomerId");
            if (Guid.TryParse(customerIdStr, out var cid))
                CustomerId = cid;

            var expiryStr = await _jsRuntime.InvokeAsync<string?>("localStorage.getItem", "authTokenExpiry");
            if (DateTime.TryParse(expiryStr, out var expiry))
                TokenExpiry = expiry;

            // If the token is already expired, log out immediately.
            if (!string.IsNullOrEmpty(Token) && IsTokenExpired())
            {
                await LogoutAsync();
            }
        }
        catch
        {
            // JS interop not available during prerender
        }
    }

    public async Task LoginAsync(string token, string fullName, string role, Guid? brandId, Guid userId, Guid? customerId = null, DateTime? tokenExpiry = null)
    {
        Token = token;
        FullName = fullName;
        Role = role;
        BrandId = brandId;
        UserId = userId;
        CustomerId = customerId;
        TokenExpiry = tokenExpiry;

        await _jsRuntime.InvokeVoidAsync("localStorage.setItem", "authToken", token);
        await _jsRuntime.InvokeVoidAsync("localStorage.setItem", "authFullName", fullName);
        await _jsRuntime.InvokeVoidAsync("localStorage.setItem", "authRole", role);
        await _jsRuntime.InvokeVoidAsync("localStorage.setItem", "authBrandId", brandId?.ToString() ?? "");
        await _jsRuntime.InvokeVoidAsync("localStorage.setItem", "authUserId", userId.ToString());
        await _jsRuntime.InvokeVoidAsync("localStorage.setItem", "authCustomerId", customerId?.ToString() ?? "");
        await _jsRuntime.InvokeVoidAsync("localStorage.setItem", "authTokenExpiry", tokenExpiry?.ToString("O") ?? "");

        OnAuthStateChanged?.Invoke();
    }

    public async Task LogoutAsync()
    {
        Token = null;
        FullName = null;
        Role = null;
        BrandId = null;
        UserId = null;
        CustomerId = null;
        TokenExpiry = null;

        try
        {
            await _jsRuntime.InvokeVoidAsync("localStorage.removeItem", "authToken");
            await _jsRuntime.InvokeVoidAsync("localStorage.removeItem", "authFullName");
            await _jsRuntime.InvokeVoidAsync("localStorage.removeItem", "authRole");
            await _jsRuntime.InvokeVoidAsync("localStorage.removeItem", "authBrandId");
            await _jsRuntime.InvokeVoidAsync("localStorage.removeItem", "authUserId");
            await _jsRuntime.InvokeVoidAsync("localStorage.removeItem", "authCustomerId");
            await _jsRuntime.InvokeVoidAsync("localStorage.removeItem", "authTokenExpiry");
            await StopIdleTimerAsync();
        }
        catch
        {
            // JS interop may not be available
        }

        OnAuthStateChanged?.Invoke();
        try
        {
            _navigation.NavigateTo("/login", forceLoad: true);
        }
        catch (InvalidOperationException)
        {
            // NavigationManager may not be initialized during prerender.
            // The component will detect the logged-out state on next render.
        }
    }

    public async Task<string?> GetTokenAsync()
    {
        if (Token == null)
            await InitializeAsync();

        if (IsTokenExpired())
        {
            await LogoutAsync();
            return null;
        }

        return Token;
    }

    public bool IsTokenExpired()
    {
        if (string.IsNullOrEmpty(Token))
            return true;

        if (TokenExpiry.HasValue)
            return TokenExpiry.Value <= DateTime.UtcNow;

        // If no expiry is stored, treat token as expired to force re-login.
        return true;
    }

    public async Task StartIdleTimerAsync()
    {
        if (!IsLoggedIn)
            return;

        try
        {
            var timeoutMs = IdleTimeoutMinutes * 60 * 1000;
            await _jsRuntime.InvokeVoidAsync("noncashIdleTimer.start", DotNetObjectReference.Create(this), timeoutMs);
        }
        catch
        {
            // JS interop may not be available
        }
    }

    public async Task StopIdleTimerAsync()
    {
        try
        {
            await _jsRuntime.InvokeVoidAsync("noncashIdleTimer.stop");
        }
        catch
        {
            // JS interop may not be available
        }
    }

    /// <summary>
    /// Redirects to /login, suppressing the exception that occurs when NavigationManager
    /// is not yet initialized during Blazor prerender.
    /// </summary>
    public void NavigateToLogin()
    {
        try
        {
            _navigation.NavigateTo("/login", forceLoad: true);
        }
        catch (NavigationException)
        {
            // Thrown during prerender when NavigationManager cannot navigate.
            // The next interactive render cycle will redirect the user.
        }
        catch (InvalidOperationException)
        {
            // NavigationManager may not be initialized yet.
        }
    }

    [JSInvokable]
    public async Task OnIdleTimeout()
    {
        await LogoutAsync();
    }
}
