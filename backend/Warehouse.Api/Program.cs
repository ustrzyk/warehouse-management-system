
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.IdentityModel.Tokens;

using Warehouse.Api.Authentication;
using Warehouse.Api.ExceptionHandling;
using Warehouse.Application.Interfaces.Services;
using Warehouse.Application.Services;
using Warehouse.Infrastructure;
using Warehouse.Infrastructure.Identity;

var builder = WebApplication.CreateBuilder(args);

// Controllers and OpenAPI
builder.Services.AddControllers();
builder.Services.AddOpenApi();

// Global exception handling
builder.Services.AddExceptionHandler<BusinessExceptionHandler>();
builder.Services.AddProblemDetails();

// SQL Server
var connectionString =
    builder.Configuration.GetConnectionString("WarehouseDb")
    ?? throw new InvalidOperationException(
        "Connection string 'WarehouseDb' not found.");

builder.Services.AddInfrastructure(connectionString);

// JWT configuration
var jwtKey = builder.Configuration["Jwt:Key"]
    ?? throw new InvalidOperationException("Brak Jwt:Key.");

var jwtIssuer = builder.Configuration["Jwt:Issuer"]
    ?? throw new InvalidOperationException("Brak Jwt:Issuer.");

var jwtAudience = builder.Configuration["Jwt:Audience"]
    ?? throw new InvalidOperationException("Brak Jwt:Audience.");

if (Encoding.UTF8.GetByteCount(jwtKey) < 32)
{
    throw new InvalidOperationException(
        "Klucz JWT musi miec co najmniej 32 bajty.");
}

// JWT authentication
builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.MapInboundClaims = false;

        options.TokenValidationParameters =
            new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidIssuer = jwtIssuer,

                ValidateAudience = true,
                ValidAudience = jwtAudience,

                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(
                    Encoding.UTF8.GetBytes(jwtKey)),

                ValidateLifetime = true,
                RequireExpirationTime = true,
                ClockSkew = TimeSpan.FromSeconds(30),

                NameClaimType = ClaimTypes.Name,
                RoleClaimType = ClaimTypes.Role
            };

        options.Events = new JwtBearerEvents
        {
            OnTokenValidated = async context =>
            {
                var userId = context.Principal?
                    .FindFirstValue(ClaimTypes.NameIdentifier);

                var userManager = context.HttpContext
                    .RequestServices
                    .GetRequiredService<UserManager<ApplicationUser>>();

                var user = string.IsNullOrEmpty(userId)
                    ? null
                    : await userManager.FindByIdAsync(userId);

                if (user is null || !user.IsActive)
                {
                    context.Fail(
                        "Konto nieaktywne lub nie istnieje.");
                }
            }
        };
    });

// Require authentication by default
builder.Services.AddAuthorization(options =>
{
    options.FallbackPolicy =
        new AuthorizationPolicyBuilder()
            .RequireAuthenticatedUser()
            .Build();
});

// JWT token generator
builder.Services.AddScoped<JwtTokenService>();

// Application services
builder.Services.AddScoped<
    IProductCategoryService,
    ProductCategoryService>();

builder.Services.AddScoped<
    IUnitOfMeasureService,
    UnitOfMeasureService>();

builder.Services.AddScoped<
    IProductService,
    ProductService>();

builder.Services.AddScoped<
    IProductBatchService,
    ProductBatchService>();

builder.Services.AddScoped<
    ILogisticUnitService,
    LogisticUnitService>();

builder.Services.AddScoped<
    ILogisticUnitItemService,
    LogisticUnitItemService>();

builder.Services.AddScoped<
    IWarehouseService,
    WarehouseService>();

builder.Services.AddScoped<
    IWarehouseZoneService,
    WarehouseZoneService>();

builder.Services.AddScoped<
    IStorageLocationService,
    StorageLocationService>();

builder.Services.AddScoped<
    IGoodsReceiptService,
    GoodsReceiptService>();

builder.Services.AddScoped<
    IWarehouseOrderService,
    WarehouseOrderService>();

builder.Services.AddScoped<
    IPickingService,
    PickingService>();

builder.Services.AddScoped<
    IShipmentService,
    ShipmentService>();

var app = builder.Build();

// Development identity seeding
if (app.Environment.IsDevelopment())
{
    await IdentitySeeder.SeedAsync(
        app.Services,
        app.Configuration);
}

// Global exception middleware
app.UseExceptionHandler();

// OpenAPI in Development
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi().AllowAnonymous();
}

// HTTP pipeline
app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();
