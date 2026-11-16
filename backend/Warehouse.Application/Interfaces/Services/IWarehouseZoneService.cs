
using Warehouse.Application.DTOs.Common;
using Warehouse.Application.DTOs.WarehouseZones;

namespace Warehouse.Application.Interfaces.Services;

public interface IWarehouseZoneService
{
    Task<PagedResult<WarehouseZoneDto>> GetPageAsync(
        WarehouseZoneListQuery query,
        CancellationToken cancellationToken);

    Task<WarehouseZoneDto?> GetByIdAsync(
        int id,
        CancellationToken cancellationToken);

    Task<WarehouseZoneDto> CreateAsync(
        CreateWarehouseZoneRequest request,
        CancellationToken cancellationToken);

    Task<WarehouseZoneDto?> UpdateAsync(
        int id,
        UpdateWarehouseZoneRequest request,
        CancellationToken cancellationToken);

    Task<bool> DeactivateAsync(
        int id,
        CancellationToken cancellationToken);
}
