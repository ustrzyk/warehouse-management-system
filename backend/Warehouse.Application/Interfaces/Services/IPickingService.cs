
using Warehouse.Application.DTOs.Common;
using Warehouse.Application.DTOs.Picking;

namespace Warehouse.Application.Interfaces.Services;

public interface IPickingService
{
    Task<PickingOperationDto?> CreateAsync(
        int orderId,
        CreatePickingRequest request,
        CancellationToken cancellationToken);

    Task<PagedResult<PickingOperationDto>?> GetPageAsync(
        int orderId,
        PickingListQuery query,
        CancellationToken cancellationToken);

    Task<PickingOperationDto?> GetByIdAsync(
        int orderId,
        int pickingId,
        CancellationToken cancellationToken);
}
