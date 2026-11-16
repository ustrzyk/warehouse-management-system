
using Warehouse.Application.DTOs.Warehouses;
using WarehouseEntity = global::Warehouse.Domain.Entities.Warehouse;

namespace Warehouse.Application.Interfaces.Repositories;

public interface IWarehouseRepository
{
    Task<(IReadOnlyList<WarehouseEntity> Items, int TotalCount)>
        GetPageAsync(
            WarehouseListQuery query,
            CancellationToken cancellationToken);

    Task<WarehouseEntity?> GetByIdAsync(
        int id,
        CancellationToken cancellationToken);

    Task<WarehouseEntity?> GetTrackedByIdAsync(
        int id,
        CancellationToken cancellationToken);

    Task<bool> CodeExistsAsync(
        string code,
        int? excludeId,
        CancellationToken cancellationToken);

    Task<bool> HasLocatedLogisticUnitsAsync(
        int warehouseId,
        CancellationToken cancellationToken);

    Task AddAsync(
        WarehouseEntity warehouse,
        CancellationToken cancellationToken);

    Task SaveChangesAsync(
        CancellationToken cancellationToken);
}
