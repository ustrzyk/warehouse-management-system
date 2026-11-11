
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Warehouse.Application.Interfaces.Repositories;
using Warehouse.Infrastructure.Persistence;
using Warehouse.Infrastructure.Repositories;

namespace Warehouse.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        string connectionString)
    {
        services.AddDbContext<WarehouseDbContext>(
            options => options.UseSqlServer(connectionString));

        services.AddScoped<
            IProductCategoryRepository,
            ProductCategoryRepository>();

        return services;
    }
}
