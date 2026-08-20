using MudBlazor.Services;
using NonCash.Web.Components;
using NonCash.Core.Configuration;
using NonCash.Web.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddMudServices();

// Client auth state service
builder.Services.AddScoped<NonCash.Web.Services.ClientAuthService>();

// Environment configuration
builder.Services.Configure<EnvironmentConfig>(builder.Configuration.GetSection(EnvironmentConfig.SectionName));
var environmentName = builder.Configuration[$"{EnvironmentConfig.SectionName}:Name"] ?? "dev";

// HTTP client for API calls
var apiBaseUrl = builder.Configuration[$"ApiBaseUrls:{environmentName}"]
    ?? builder.Configuration["ApiBaseUrl"]
    ?? "https://localhost:7001/";
builder.Services.AddTransient<AuthHttpHandler>();
builder.Services.AddHttpClient("NonCashAPI", client =>
{
    client.BaseAddress = new Uri(apiBaseUrl);
}).AddHttpMessageHandler<AuthHttpHandler>();
builder.Services.AddScoped(sp => sp.GetRequiredService<IHttpClientFactory>().CreateClient("NonCashAPI"));

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseAntiforgery();
app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
