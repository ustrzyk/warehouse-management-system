
using Warehouse.Application.DTOs.Picking;
using Warehouse.Domain.Entities;

namespace Warehouse.Application.Interfaces.Repositories;

public interface IPickingRepository
{
    Task<bool> OrderExistsAsync(
        int orderId,
        CancellationToken cancellationToken);

    Task<PickingOperation?> GetByRequestIdAsync(
        Guid requestId,
        CancellationToken cancellationToken);

    Task<WarehouseOrderItem?> GetTrackedOrderItemAsync(
        int orderId,
        int orderItemId,
        CancellationToken cancellationToken);

    Task<LogisticUnitItem?> GetTrackedPalletItemAsync(
        int logisticUnitItemId,
        CancellationToken cancellationToken);

    Task<decimal> GetPickedQuantityAsync(
        int orderItemId,
        CancellationToken cancellationToken);

    Task AddAsync(
        PickingOperation operation,
        CancellationToken cancellationToken);

    Task SaveChangesAsync(
        CancellationToken cancellationToken);

    Task<(IReadOnlyList<PickingOperation> Items, int TotalCount)>
        GetPageAsync(
            int orderId,
            PickingListQuery query,
            CancellationToken cancellationToken);

    Task<PickingOperation?> GetByIdAsync(
        int orderId,
        int pickingId,
        CancellationToken cancellationToken);
}
