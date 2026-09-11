using FluentAssertions;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Configuration;
using Microsoft.JSInterop;
using NonCash.Web.Services;

namespace NonCash.IntegrationTests.Services;

/// <summary>Locks the client-auth lifecycle principle: the prerender pass (no JS interop) must
/// stay side-effect free, while the interactive circuit keeps its real logout behaviour.</summary>
public class ClientAuthServiceTests
{
    private static IConfiguration EmptyConfig() =>
        new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>()).Build();

    [Fact]
    public async Task GetTokenAsync_DuringPrerender_ReturnsNullWithoutLoggingOutOrNavigating()
    {
        var navigation = new RecordingNavigationManager();
        var service = new ClientAuthService(new PrerenderJsRuntime(), navigation, EmptyConfig());

        var token = await service.GetTokenAsync();

        token.Should().BeNull();
        service.IsLoggedIn.Should().BeFalse();
        navigation.Navigations.Should().BeEmpty();
    }

    [Fact]
    public async Task GetTokenAsync_WithAValidStoredToken_ReturnsItWithoutNavigating()
    {
        var storage = new Dictionary<string, string?>
        {
            ["authToken"] = "stored-token",
            ["authTokenExpiry"] = DateTime.UtcNow.AddHours(1).ToString("O")
        };
        var navigation = new RecordingNavigationManager();
        var service = new ClientAuthService(new FakeJsRuntime(storage), navigation, EmptyConfig());

        var token = await service.GetTokenAsync();

        token.Should().Be("stored-token");
        navigation.Navigations.Should().BeEmpty();
    }

    [Fact]
    public async Task GetTokenAsync_WithAnExpiredStoredToken_LogsOutAndRedirects()
    {
        var storage = new Dictionary<string, string?>
        {
            ["authToken"] = "stored-token",
            ["authTokenExpiry"] = DateTime.UtcNow.AddDays(-1).ToString("O")
        };
        var navigation = new RecordingNavigationManager();
        var service = new ClientAuthService(new FakeJsRuntime(storage), navigation, EmptyConfig());

        var token = await service.GetTokenAsync();

        token.Should().BeNull();
        navigation.Navigations.Should().OnlyContain(uri => uri == "/login");
    }

    [Fact]
    public async Task GetTokenAsync_WithAnExpiryOneMinutePast_AlreadyCountsAsExpired()
    {
        // Regression: plain TryParse shifted the UTC round-trip string to local time, so at
        // UTC+7 a token one minute past expiry still looked valid for another ~7 hours.
        var storage = new Dictionary<string, string?>
        {
            ["authToken"] = "stored-token",
            ["authTokenExpiry"] = DateTime.UtcNow.AddMinutes(-1).ToString("O")
        };
        var navigation = new RecordingNavigationManager();
        var service = new ClientAuthService(new FakeJsRuntime(storage), navigation, EmptyConfig());

        var token = await service.GetTokenAsync();

        token.Should().BeNull();
        navigation.Navigations.Should().OnlyContain(uri => uri == "/login");
    }

    [Fact]
    public async Task GetTokenAsync_WithAnExpiredMemberToken_RedirectsToTheMemberLogin()
    {
        // Regression: every exit path used to point at the staff /login, so an expired member
        // session landed on a screen whose credentials it does not have.
        var storage = new Dictionary<string, string?>
        {
            ["authToken"] = "stored-token",
            ["authRole"] = "Member",
            ["authTokenExpiry"] = DateTime.UtcNow.AddDays(-1).ToString("O")
        };
        var navigation = new RecordingNavigationManager();
        var service = new ClientAuthService(new FakeJsRuntime(storage), navigation, EmptyConfig());

        var token = await service.GetTokenAsync();

        token.Should().BeNull();
        navigation.Navigations.Should().OnlyContain(uri => uri == "/member-login");
    }

    [Fact]
    public async Task GetTokenAsync_WithAnExpiredStaffToken_StillRedirectsToTheStaffLogin()
    {
        var storage = new Dictionary<string, string?>
        {
            ["authToken"] = "stored-token",
            ["authRole"] = "BrandManager",
            ["authTokenExpiry"] = DateTime.UtcNow.AddDays(-1).ToString("O")
        };
        var navigation = new RecordingNavigationManager();
        var service = new ClientAuthService(new FakeJsRuntime(storage), navigation, EmptyConfig());

        var token = await service.GetTokenAsync();

        token.Should().BeNull();
        navigation.Navigations.Should().OnlyContain(uri => uri == "/login");
    }

    [Fact]
    public async Task LoginAsync_ThenGetTokenAsync_InTheSameCircuit_ReturnsTheToken()
    {
        var storage = new Dictionary<string, string?>();
        var navigation = new RecordingNavigationManager();
        var service = new ClientAuthService(new FakeJsRuntime(storage), navigation, EmptyConfig());

        await service.LoginAsync(
            "fresh-token", "Member One", "Member", null, Guid.NewGuid(), null, DateTime.UtcNow.AddHours(1));

        (await service.GetTokenAsync()).Should().Be("fresh-token");
        navigation.Navigations.Should().BeEmpty();
    }

    /// <summary>Simulates the prerender pass, where every JS interop call throws.</summary>
    private sealed class PrerenderJsRuntime : IJSRuntime
    {
        public ValueTask<T> InvokeAsync<T>(string identifier, object?[]? args) =>
            throw new InvalidOperationException("JavaScript interop calls cannot be issued at this time.");

        public ValueTask<T> InvokeAsync<T>(string identifier, CancellationToken cancellationToken, object?[]? args) =>
            throw new InvalidOperationException("JavaScript interop calls cannot be issued at this time.");
    }

    private sealed class FakeJsRuntime : IJSRuntime
    {
        private readonly Dictionary<string, string?> _storage;

        public FakeJsRuntime(Dictionary<string, string?> storage) => _storage = storage;

        public ValueTask<T> InvokeAsync<T>(string identifier, object?[]? args) =>
            Invoke<T>(identifier, args);

        public ValueTask<T> InvokeAsync<T>(string identifier, CancellationToken cancellationToken, object?[]? args) =>
            Invoke<T>(identifier, args);

        private ValueTask<T> Invoke<T>(string identifier, object?[]? args)
        {
            switch (identifier)
            {
                case "localStorage.getItem":
                    _storage.TryGetValue((string)args![0]!, out var value);
                    return new ValueTask<T>((T)(object?)value!);
                case "localStorage.setItem":
                    _storage[(string)args![0]!] = args![1] as string;
                    return new ValueTask<T>(default(T)!);
                case "localStorage.removeItem":
                    _storage.Remove((string)args![0]!);
                    return new ValueTask<T>(default(T)!);
                default:
                    return new ValueTask<T>(default(T)!);
            }
        }
    }

    private sealed class RecordingNavigationManager : NavigationManager
    {
        public List<string> Navigations { get; } = new();

        public RecordingNavigationManager() =>
            Initialize("https://localhost/", "https://localhost/my-vouchers");

        protected override void NavigateToCore(string uri, bool forceLoad) => Navigations.Add(uri);
    }
}
