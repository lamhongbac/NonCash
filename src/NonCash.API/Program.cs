using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using NonCash.API.HostedServices;
using NonCash.API.Middleware;
using NonCash.API.RateLimiting;
using NonCash.API.Services;
using NonCash.Core.Configuration;
using NonCash.Core.Interfaces;
using NonCash.Core.Services;
using NonCash.Infrastructure.Data;
using NonCash.Infrastructure.Repositories;
using NonCash.Infrastructure.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();

// CORS: allow the NonCash.Pos PWA app (https://localhost:7200) to call the API.
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.WithOrigins("https://localhost:7200", "http://localhost:5201")
              .AllowAnyMethod()
              .AllowAnyHeader()
              .AllowCredentials();
    });
});
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new() { Title = "NonCash API", Version = "v1" });
});

// Environment configuration
builder.Services.Configure<EnvironmentConfig>(builder.Configuration.GetSection(EnvironmentConfig.SectionName));
var environmentName = builder.Configuration[$"{EnvironmentConfig.SectionName}:Name"] ?? "dev";

// Database
var connectionStringKey = environmentName.ToLowerInvariant() switch
{
    "dev" or "development" => "DevConnection",
    "pilot" => "PilotConnection",
    "production" or "prod" => "ProductionConnection",
    _ => "DefaultConnection"
};

// Blank counts as absent: the tracked appsettings files keep these keys with empty values so the
// config shape stays documented, while the real values come from user-secrets or the deployed
// appsettings.json. GetConnectionString returns "" for those, which would otherwise end the chain.
static string? Present(string? value) => string.IsNullOrWhiteSpace(value) ? null : value;

var connectionString = Present(builder.Configuration.GetConnectionString(connectionStringKey))
    ?? Present(builder.Configuration.GetConnectionString("DefaultConnection"))
    ?? Present(builder.Configuration["NONCASH_CONNECTION_STRING"])
    ?? "Host=localhost;Database=noncash;Username=postgres;Password=postgres";

builder.Services.AddDbContext<ApplicationDbContext>(options =>
{
    options.UseNpgsql(connectionString);
    if (builder.Environment.IsDevelopment())
    {
        options.EnableSensitiveDataLogging();
    }
});

// HTTP context accessor for current user service
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUserService, CurrentUserService>();

// Repository pattern
builder.Services.AddScoped(typeof(IRepository<>), typeof(Repository<>));
builder.Services.AddScoped<IBusinessRepository, BusinessRepository>();
builder.Services.AddScoped<IBrandRepository, BrandRepository>();
builder.Services.AddScoped<IOutletRepository, OutletRepository>();
builder.Services.AddScoped<ICustomerRepository, CustomerRepository>();
builder.Services.AddScoped<IBrandCustomerRepository, BrandCustomerRepository>();
builder.Services.AddScoped<IUserAccountRepository, UserAccountRepository>();
builder.Services.AddScoped<IMemberAccountRepository, MemberAccountRepository>();
builder.Services.AddScoped<IBusinessRegistrationRequestRepository, BusinessRegistrationRequestRepository>();
builder.Services.AddScoped<IVoucherPlanRepository, VoucherPlanRepository>();
builder.Services.AddScoped<IVoucherTransferRepository, VoucherTransferRepository>();
builder.Services.AddScoped<IVoucherLockRepository, VoucherLockRepository>();
builder.Services.AddScoped<IRedemptionReportRepository, RedemptionReportRepository>();

// Business services
builder.Services.AddScoped<BrandService>();
builder.Services.AddScoped<OutletService>();
builder.Services.AddScoped<CustomerService>();
builder.Services.AddScoped<UserService>();
builder.Services.AddScoped<StoreStaffService>();
builder.Services.AddScoped<IVoucherPlanService, VoucherPlanService>();
builder.Services.AddScoped<IVoucherCodeService, VoucherCodeService>();
builder.Services.AddScoped<IVoucherGenerationService, VoucherGenerationService>();
builder.Services.AddScoped<IVoucherTransferService, VoucherTransferService>();
builder.Services.AddScoped<ITransferService, TransferService>();
builder.Services.AddScoped<IApprovalService, ApprovalService>();
builder.Services.AddScoped<IPlanCloneService, PlanCloneService>();
builder.Services.AddScoped<IPurchaseService, PurchaseService>();

// ZaloPay payment options
builder.Services.Configure<ZaloPayOptions>(builder.Configuration.GetSection("ZaloPay"));
builder.Services.Configure<VoucherCodeOptions>(builder.Configuration.GetSection(VoucherCodeOptions.SectionName));
builder.Services.AddHttpClient("ZaloPay", client =>
{
    client.BaseAddress = new Uri("https://sb-openapi.zalopay.vn");
    client.Timeout = TimeSpan.FromSeconds(30);
});
builder.Services.AddScoped<IPaymentService, ZaloPayPaymentService>();

builder.Services.AddScoped<IPosService, PosService>();
builder.Services.AddScoped<IPromotionService, PromotionService>();
builder.Services.AddScoped<IDistributionReportService, DistributionReportService>();
builder.Services.AddScoped<IDistributionBatchService, DistributionBatchService>();
builder.Services.AddScoped<IUserOutletRepository, UserOutletRepository>();
// Settlement (Epic 7.2)
builder.Services.AddScoped<ISettlementService, SettlementService>();

// Prepaid credits (Epic 9) + pricing policy / batch / adjustments (Epic 10)
var creditConfig = builder.Configuration.GetSection(CreditConfig.SectionName).Get<CreditConfig>() ?? new CreditConfig();
builder.Services.AddSingleton(creditConfig);
builder.Services.AddScoped<ICreditService, CreditService>();
builder.Services.AddScoped<ICreditPolicyService, CreditPolicyService>();
builder.Services.AddScoped<IWelcomePolicyService, WelcomePolicyService>();
builder.Services.AddScoped<IContractService, ContractService>();
builder.Services.AddScoped<IContractTemplateService, ContractTemplateService>();
builder.Services.AddScoped<ISubscriptionFeePolicyService, SubscriptionFeePolicyService>();
builder.Services.AddScoped<ICreditAdjustmentService, CreditAdjustmentService>();

// Integration partners (Epic 6)
builder.Services.AddScoped<IIntegrationPartnerService, IntegrationPartnerService>();
builder.Services.AddScoped<IVoucherEventPublisher, VoucherEventPublisher>();
builder.Services.AddHttpClient("WebhookDelivery", client =>
{
    client.Timeout = TimeSpan.FromSeconds(15);
});

// Auth services
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IJwtTokenService, JwtTokenService>();

// Registration services
builder.Services.AddScoped<IRegistrationService, RegistrationService>();

// Notification services
builder.Services.Configure<SmtpOptions>(builder.Configuration.GetSection("Smtp"));
builder.Services.AddScoped<IEmailTemplateRenderer, PlaceholderEmailTemplateRenderer>();
var environmentConfig = builder.Configuration.GetSection(EnvironmentConfig.SectionName).Get<EnvironmentConfig>() ?? new EnvironmentConfig();
var smtpHost = builder.Configuration["Smtp:Host"];
// Email toggle: explicit flag wins everywhere; when absent, dev defaults to suppressed and other environments to enabled.
var emailEnabled = builder.Configuration.GetValue<bool?>("Notifications:EmailEnabled") ?? !environmentConfig.IsDev;
if (emailEnabled && !string.IsNullOrWhiteSpace(smtpHost))
{
    builder.Services.AddScoped<INotificationService, EmailNotificationService>();
}
else
{
    // File sink: notifications (including magic-link URLs) are appended to logs/notifications.log
    // instead of the console. Active when Notifications:EmailEnabled is off or SMTP is unconfigured.
    builder.Services.AddScoped<INotificationService, FileNotificationService>();
}

// Import services
builder.Services.AddScoped<ICustomerImportService, CsvCustomerImportService>();

// Image storage (Epic 8.1) — toggle via MediaServiceConfig:ImageStorage ("MSA" or "Local")
var imageStorageMode = builder.Configuration["MediaServiceConfig:ImageStorage"] ?? "Local";
if (imageStorageMode.Equals("MSA", StringComparison.OrdinalIgnoreCase))
{
    // MSA media service: uploads to remote storage, returns RelativeUrl
    builder.Services.AddHttpClient<MsaMediaClient>();
    builder.Services.AddScoped<IImageStorageService, MsaImageStorageService>();
}
else
{
    // Local fallback: stores to wwwroot/uploads/
    var webRootPath = builder.Environment.WebRootPath
        ?? Path.Combine(builder.Environment.ContentRootPath, "wwwroot");
    builder.Services.AddSingleton<IImageStorageService>(new LocalStorageImageService(webRootPath));
}

// Document storage (signed contracts) — same MSA/Local toggle as images
if (imageStorageMode.Equals("MSA", StringComparison.OrdinalIgnoreCase))
{
    builder.Services.AddScoped<IDocumentStorageService, MsaDocumentStorageService>();
}
else
{
    var documentWebRootPath = builder.Environment.WebRootPath
        ?? Path.Combine(builder.Environment.ContentRootPath, "wwwroot");
    builder.Services.AddSingleton<IDocumentStorageService>(new LocalStorageDocumentService(documentWebRootPath));
}

// JWT Authentication
// Resolved eagerly and outside the options lambda: AddJwtBearer configures lazily, so a missing or
// too-short key would otherwise surface on the first authenticated request instead of at startup.
var jwtSigningKey = JwtSigningKey.Resolve(builder.Configuration);
builder.Services.AddAuthentication("Bearer")
    .AddJwtBearer("Bearer", options =>
    {
        var jwtConfig = builder.Configuration.GetSection("Jwt");
        options.RequireHttpsMetadata = false;
        options.TokenValidationParameters = new()
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwtConfig["Issuer"] ?? "NonCash",
            ValidAudience = jwtConfig["Audience"] ?? "NonCash.Users",
            IssuerSigningKey = jwtSigningKey,
            ClockSkew = TimeSpan.FromMinutes(5)
        };
    });

builder.Services.AddAuthorization();

// Hosted services
builder.Services.AddHostedService<LockCleanupService>();
builder.Services.AddHostedService<TransferExpirySweepService>();
builder.Services.AddHostedService<WebhookDeliveryService>();
builder.Services.AddHostedService<CreditExpirySweepService>();

// Health checks
builder.Services.AddHealthChecks()
    .AddDbContextCheck<ApplicationDbContext>("postgresql");

// Rate limiter (CR-2026-09-07-18): protect staff-login from credential stuffing. 5 requests
// per minute per client IP; other endpoints are unaffected. CR-04 will broaden this.
// CR-2026-09-09-27: member-auth covers customer sign-in and the recovery link, which is both a
// credential-stuffing target and a mail-sending endpoint.
// CR-2026-09-06-04: auth-recovery covers the remaining anonymous auth endpoints and pos-outlet
// covers every /api/v1/pos/* call. Both are partitioned, because AddFixedWindowLimiter hands every
// caller one shared bucket — a single abuser could then lock out recovery for the whole platform.
builder.Services.AddRateLimiter(options =>
{
    options.AddFixedWindowLimiter("staff-login", opt =>
    {
        opt.PermitLimit = 5;
        opt.Window = TimeSpan.FromMinutes(1);
        opt.QueueLimit = 0;
    });
    options.AddFixedWindowLimiter("member-auth", opt =>
    {
        opt.PermitLimit = 5;
        opt.Window = TimeSpan.FromMinutes(1);
        opt.QueueLimit = 0;
    });
    options.AddPolicy("auth-recovery", httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            RateLimitPartitionKeys.ForClientIp(httpContext),
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 5,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0
            }));
    // POS is keyed, not IP-bound: every terminal in a store sits behind one NAT address, so an IP
    // bucket would let a single busy register starve the rest of the store. UseRateLimiter runs
    // before ApiKeyMiddleware, so the outlet is not resolved yet — partition on the raw header.
    options.AddPolicy("pos-outlet", httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            RateLimitPartitionKeys.ForPosApiKey(httpContext.Request.Headers["X-API-Key"].ToString()),
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 120,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0
            }));
    // Minting a code is a cheap HMAC but a replayable credential, so a stolen-session script must
    // not be able to farm fresh codes without limit. Claims are unavailable this early in the
    // pipeline (UseRateLimiter precedes UseAuthentication), so partition on client IP.
    options.AddPolicy("member-code", httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            RateLimitPartitionKeys.ForClientIp(httpContext),
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 10,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0
            }));
    options.OnRejected = async (context, token) =>
    {
        // OnRejectedContext carries no policy name, so read it back off the endpoint that was throttled.
        var policyName = context.HttpContext.GetEndpoint()?
            .Metadata.GetMetadata<EnableRateLimitingAttribute>()?.PolicyName;
        var message = policyName == "pos-outlet"
            ? "This terminal has sent too many voucher requests in the last minute. Wait 60 seconds, then scan "
              + "the code again — requests sent before then are rejected."
            : policyName == "member-code"
                ? "You asked for new voucher codes too many times. Wait 60 seconds, then tap the voucher again."
                : "Too many attempts. Please wait a minute and try again.";

        context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
        context.HttpContext.Response.ContentType = "application/json";
        await context.HttpContext.Response.WriteAsJsonAsync(new { error = message }, token);
    };
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseCors();
app.UseRateLimiter();
app.UseStaticFiles(); // Serve uploaded images from wwwroot/uploads/
app.UseAuthentication();
app.UseMiddleware<BrandScopeMiddleware>();
app.UseMiddleware<IntegrationApiKeyMiddleware>();
// POS X-API-Key gate (CR-18): validates the key and attaches pos.outlet_id / pos.brand_id.
// PosController has no [Authorize] by design — this middleware IS its auth boundary.
app.UseMiddleware<ApiKeyMiddleware>();
app.UseAuthorization();

app.MapControllers();
app.MapHealthChecks("/health");

// Seed admin account (retry while the database is still coming up, e.g. right after a machine/service restart)
for (var attempt = 1; ; attempt++)
{
    try
    {
        await NonCash.Infrastructure.Data.DatabaseSeeder.SeedAdminAsync(app.Services);
        break;
    }
    catch (Exception ex) when (attempt < 5)
    {
        app.Logger.LogWarning(ex, "Database not ready for seeding (attempt {Attempt}/5); retrying in 3s...", attempt);
        await Task.Delay(TimeSpan.FromSeconds(3));
    }
}

app.Run();
