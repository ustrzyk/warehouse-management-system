
using Warehouse.Application.DTOs.WarehouseZones;
using Warehouse.Domain.Entities;

namespace Warehouse.Application.Interfaces.Repositories;

public interface IWarehouseZoneRepository
{
    Task<(IReadOnlyList<WarehouseZone> Items, int TotalCount)>
        GetPageAsync(
            WarehouseZoneListQuery query,
            CancellationToken cancellationToken);

    Task<WarehouseZone?> GetByIdAsync(
        int id,
        CancellationToken cancellationToken);

    Task<WarehouseZone?> GetTrackedByIdAsync(
        int id,
        CancellationToken cancellationToken);

    Task<bool> WarehouseIsActiveAsync(
        int warehouseId,
        CancellationToken cancellationToken);

    Task<bool> CodeExistsAsync(
        int warehouseId,
        string code,
        int? excludeId,
        CancellationToken cancellationToken);

    Task<bool> HasLocatedLogisticUnitsAsync(
        int zoneId,
        CancellationToken cancellationToken);

    Task AddAsync(
        WarehouseZone zone,
        CancellationToken cancellationToken);

    Task SaveChangesAsync(
        CancellationToken cancellationToken);
}
