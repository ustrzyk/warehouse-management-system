
using Warehouse.Api.ExceptionHandling;
using Warehouse.Application.Interfaces.Services;
using Warehouse.Application.Services;
using Warehouse.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

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


var app = builder.Build();

// Global exception middleware
app.UseExceptionHandler();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();

app.Run();
