using Blazored.LocalStorage;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using MudBlazor.Services;
using NonCash.Pos.Services;
using NonCash.PosApp;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

// MudBlazor
builder.Services.AddMudServices();

// Local storage (for terminal provisioning — API key stored once)
builder.Services.AddBlazoredLocalStorage();

// Provisioning + auth services (scoped = per user session in WASM)
builder.Services.AddScoped<ProvisioningService>();
builder.Services.AddScoped<PosAuthService>();

// POS API client — dogfoods the public integration contract (X-API-Key per outlet).
// Base address points to the NonCash API server (CORS must be configured on the API side).
var apiBaseUrl = builder.Configuration["ApiBaseUrl"] ?? "https://localhost:7107/";
builder.Services.AddScoped(sp =>
{
    var http = new HttpClient { BaseAddress = new Uri(apiBaseUrl) };
    return http;
});
builder.Services.AddScoped<PosApiClient>();

await builder.Build().RunAsync();
