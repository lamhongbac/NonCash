using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace NonCash.Web.Services;

public class ClientAuthService
{
    private readonly IJSRuntime _jsRuntime;
    private readonly NavigationManager _navigation;

    public event Action? OnAuthStateChanged;

    public string? Token { get; private set; }
    public string? FullName { get; private set; }
    public string? Role { get; private set; }
    public Guid? BrandId { get; private set; }
    public Guid? UserId { get; private set; }
    public Guid? CustomerId { get; private set; }
    public bool IsLoggedIn => !string.IsNullOrEmpty(Token);
    public bool IsMember => Role?.Equals("Member", StringComparison.OrdinalIgnoreCase) == true;

    public ClientAuthService(IJSRuntime jsRuntime, NavigationManager navigation)
    {
        _jsRuntime = jsRuntime;
        _navigation = navigation;
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
        }
        catch
        {
            // JS interop not available during prerender
        }
    }

    public async Task LoginAsync(string token, string fullName, string role, Guid? brandId, Guid userId, Guid? customerId = null)
    {
        Token = token;
        FullName = fullName;
        Role = role;
        BrandId = brandId;
        UserId = userId;
        CustomerId = customerId;

        await _jsRuntime.InvokeVoidAsync("localStorage.setItem", "authToken", token);
        await _jsRuntime.InvokeVoidAsync("localStorage.setItem", "authFullName", fullName);
        await _jsRuntime.InvokeVoidAsync("localStorage.setItem", "authRole", role);
        await _jsRuntime.InvokeVoidAsync("localStorage.setItem", "authBrandId", brandId?.ToString() ?? "");
        await _jsRuntime.InvokeVoidAsync("localStorage.setItem", "authUserId", userId.ToString());
        await _jsRuntime.InvokeVoidAsync("localStorage.setItem", "authCustomerId", customerId?.ToString() ?? "");

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

        try
        {
            await _jsRuntime.InvokeVoidAsync("localStorage.removeItem", "authToken");
            await _jsRuntime.InvokeVoidAsync("localStorage.removeItem", "authFullName");
            await _jsRuntime.InvokeVoidAsync("localStorage.removeItem", "authRole");
            await _jsRuntime.InvokeVoidAsync("localStorage.removeItem", "authBrandId");
            await _jsRuntime.InvokeVoidAsync("localStorage.removeItem", "authUserId");
            await _jsRuntime.InvokeVoidAsync("localStorage.removeItem", "authCustomerId");
        }
        catch
        {
            // JS interop may not be available
        }

        OnAuthStateChanged?.Invoke();
        _navigation.NavigateTo("/");
    }

    public async Task<string?> GetTokenAsync()
    {
        if (Token == null)
            await InitializeAsync();
        return Token;
    }
}
