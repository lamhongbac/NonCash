using Microsoft.EntityFrameworkCore;
using NonCash.API.Middleware;
using NonCash.API.Services;
using NonCash.Core.Interfaces;
using NonCash.Core.Services;
using NonCash.Infrastructure.Data;
using NonCash.Infrastructure.Repositories;
using NonCash.Infrastructure.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new() { Title = "NonCash API", Version = "v1" });
});

// Database
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? builder.Configuration["NONCASH_CONNECTION_STRING"]
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
builder.Services.AddScoped<IBrandRepository, BrandRepository>();
builder.Services.AddScoped<IOutletRepository, OutletRepository>();
builder.Services.AddScoped<ICustomerRepository, CustomerRepository>();
builder.Services.AddScoped<IUserAccountRepository, UserAccountRepository>();
builder.Services.AddScoped<IBrandRegistrationRequestRepository, BrandRegistrationRequestRepository>();
builder.Services.AddScoped<IVoucherPlanRepository, VoucherPlanRepository>();

// Business services
builder.Services.AddScoped<BrandService>();
builder.Services.AddScoped<OutletService>();
builder.Services.AddScoped<CustomerService>();
builder.Services.AddScoped<UserService>();
builder.Services.AddScoped<IVoucherPlanService, VoucherPlanService>();
builder.Services.AddScoped<IVoucherCodeService, VoucherCodeService>();
builder.Services.AddScoped<IVoucherGenerationService, VoucherGenerationService>();

// Auth services
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IJwtTokenService, JwtTokenService>();

// Registration services
builder.Services.AddScoped<IRegistrationService, RegistrationService>();

// Notification services
builder.Services.AddScoped<INotificationService, ConsoleNotificationService>();

// Import services
builder.Services.AddScoped<ICustomerImportService, CsvCustomerImportService>();

// JWT Authentication
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
            IssuerSigningKey = new Microsoft.IdentityModel.Tokens.SymmetricSecurityKey(
                System.Text.Encoding.UTF8.GetBytes(jwtConfig["Key"] ?? "noncash-dev-key-min-32-bytes-long!!")),
            ClockSkew = TimeSpan.FromMinutes(5)
        };
    });

builder.Services.AddAuthorization();

// Health checks
builder.Services.AddHealthChecks()
    .AddDbContextCheck<ApplicationDbContext>("postgresql");

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseAuthentication();
app.UseMiddleware<BrandScopeMiddleware>();
app.UseAuthorization();

app.MapControllers();
app.MapHealthChecks("/health");

// Seed admin account
await NonCash.Infrastructure.Data.DatabaseSeeder.SeedAdminAsync(app.Services);

app.Run();
