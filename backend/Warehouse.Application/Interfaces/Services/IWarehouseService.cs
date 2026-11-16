
using Warehouse.Application.DTOs.Common;
using Warehouse.Application.DTOs.Warehouses;

namespace Warehouse.Application.Interfaces.Services;

public interface IWarehouseService
{
    Task<PagedResult<WarehouseDto>> GetPageAsync(
        WarehouseListQuery query,
        CancellationToken cancellationToken);

    Task<WarehouseDto?> GetByIdAsync(
        int id,
        CancellationToken cancellationToken);

    Task<WarehouseDto> CreateAsync(
        CreateWarehouseRequest request,
        CancellationToken cancellationToken);

    Task<WarehouseDto?> UpdateAsync(
        int id,
        UpdateWarehouseRequest request,
        CancellationToken cancellationToken);

    Task<bool> DeactivateAsync(
        int id,
        CancellationToken cancellationToken);
}
