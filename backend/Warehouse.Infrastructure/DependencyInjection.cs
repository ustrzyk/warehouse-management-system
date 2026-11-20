
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Warehouse.Application.Interfaces.Repositories;
using Warehouse.Infrastructure.Persistence;
using Warehouse.Infrastructure.Repositories;
using Microsoft.AspNetCore.Identity;
using Warehouse.Infrastructure.Identity;


namespace Warehouse.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        string connectionString)
    {
        services.AddDbContext<WarehouseDbContext>(
            options => options.UseSqlServer(connectionString));

        services.AddIdentityCore<ApplicationUser>(options =>
        {
            // Password requirements
            options.Password.RequiredLength = 12;
            options.Password.RequireDigit = true;
            options.Password.RequireLowercase = true;
            options.Password.RequireUppercase = true;
            options.Password.RequireNonAlphanumeric = true;

            // Users
            options.User.RequireUniqueEmail = true;

            // Lockout
            options.Lockout.DefaultLockoutTimeSpan =
                TimeSpan.FromMinutes(15);

            options.Lockout.MaxFailedAccessAttempts = 5;
        })
        .AddRoles<IdentityRole>()
        .AddEntityFrameworkStores<WarehouseDbContext>();


        services.AddScoped<
            IProductCategoryRepository,
            ProductCategoryRepository>();

        services.AddScoped<
            IUnitOfMeasureRepository,
            UnitOfMeasureRepository>();

        services.AddScoped<
            IProductRepository,
            ProductRepository>();

        services.AddScoped<
            IProductBatchRepository,
            ProductBatchRepository>();

        services.AddScoped<
            ILogisticUnitRepository,
            LogisticUnitRepository>();

        services.AddScoped<
            ILogisticUnitItemRepository,
            LogisticUnitItemRepository>();

        services.AddScoped<
            IWarehouseRepository,
            WarehouseRepository>();

        services.AddScoped<
            IWarehouseZoneRepository,
            WarehouseZoneRepository>();

        services.AddScoped<
            IStorageLocationRepository,
            StorageLocationRepository>();

        services.AddScoped<
            IGoodsReceiptRepository,
            GoodsReceiptRepository>();

        services.AddScoped<
            IWarehouseOrderRepository,
            WarehouseOrderRepository>();

        services.AddScoped<
            IPickingRepository,
            PickingRepository>();

        services.AddScoped<
            IShipmentRepository,
            ShipmentRepository>();



        return services;
    }
}
