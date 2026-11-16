
using Warehouse.Application.DTOs.StorageLocations;
using Warehouse.Domain.Entities;

namespace Warehouse.Application.Interfaces.Repositories;

public interface IStorageLocationRepository
{
    Task<(IReadOnlyList<StorageLocation> Items, int TotalCount)>
        GetPageAsync(
            StorageLocationListQuery query,
            CancellationToken cancellationToken);

    Task<StorageLocation?> GetByIdAsync(
        int id,
        CancellationToken cancellationToken);

    Task<StorageLocation?> GetTrackedByIdAsync(
        int id,
        CancellationToken cancellationToken);

    Task<bool> ZoneAndWarehouseAreActiveAsync(
        int warehouseZoneId,
        CancellationToken cancellationToken);

    Task<bool> CodeExistsAsync(
        int warehouseZoneId,
        string code,
        int? excludeId,
        CancellationToken cancellationToken);

    Task<bool> HasAssignedLogisticUnitsAsync(
        int storageLocationId,
        CancellationToken cancellationToken);

    Task AddAsync(
        StorageLocation location,
        CancellationToken cancellationToken);

    Task SaveChangesAsync(
        CancellationToken cancellationToken);
}
