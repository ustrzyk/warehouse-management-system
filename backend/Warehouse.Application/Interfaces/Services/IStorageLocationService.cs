
using Warehouse.Application.DTOs.Common;
using Warehouse.Application.DTOs.StorageLocations;

namespace Warehouse.Application.Interfaces.Services;

public interface IStorageLocationService
{
    Task<PagedResult<StorageLocationDto>> GetPageAsync(
        StorageLocationListQuery query,
        CancellationToken cancellationToken);

    Task<StorageLocationDto?> GetByIdAsync(
        int id,
        CancellationToken cancellationToken);

    Task<StorageLocationDto> CreateAsync(
        CreateStorageLocationRequest request,
        CancellationToken cancellationToken);

    Task<StorageLocationDto?> UpdateAsync(
        int id,
        UpdateStorageLocationRequest request,
        CancellationToken cancellationToken);

    Task<bool> DeactivateAsync(
        int id,
        CancellationToken cancellationToken);
}
