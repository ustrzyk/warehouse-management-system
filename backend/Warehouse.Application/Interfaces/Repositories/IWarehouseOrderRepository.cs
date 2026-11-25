
using Warehouse.Application.DTOs.WarehouseOrders;
using Warehouse.Domain.Entities;

namespace Warehouse.Application.Interfaces.Repositories;

public interface IWarehouseOrderRepository
{
    Task<(IReadOnlyList<WarehouseOrder> Items, int TotalCount)>
        GetPageAsync(
            WarehouseOrderListQuery query,
            CancellationToken cancellationToken);

    Task<WarehouseOrder?> GetByIdAsync(
        int id,
        CancellationToken cancellationToken);

    Task<WarehouseOrder?> GetTrackedByIdAsync(
        int id,
        CancellationToken cancellationToken);

    Task<bool> WarehouseIsActiveAsync(
        int warehouseId,
        CancellationToken cancellationToken);

    Task<bool> NumberExistsAsync(
        string number,
        CancellationToken cancellationToken);

    Task<bool> ProductIsActiveAsync(
        int productId,
        CancellationToken cancellationToken);

    Task<bool> BatchIsActiveForProductAsync(
        int batchId,
        int productId,
        CancellationToken cancellationToken);

    Task<bool> ItemExistsAsync(
        int orderId,
        int productId,
        int? batchId,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<WarehouseOrderItem>> GetItemsAsync(
        int orderId,
        CancellationToken cancellationToken);

    Task<WarehouseOrderItem?> GetItemByIdAsync(
        int orderId,
        int itemId,
        CancellationToken cancellationToken);

    Task AddAsync(
        WarehouseOrder order,
        CancellationToken cancellationToken);

    Task AddItemAsync(
        WarehouseOrderItem item,
        CancellationToken cancellationToken);

    Task SaveChangesAsync(
        CancellationToken cancellationToken);

    Task<WarehouseOrder?> GetTrackedWithItemsAsync(
        int id,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<WarehouseStockBalance>> GetAvailableStockAsync(
        int warehouseId,
        IReadOnlyCollection<int> productIds,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<WarehouseStockBalance>>
        GetOutstandingReleasedStockAsync(
            int warehouseId,
            IReadOnlyCollection<int> productIds,
            CancellationToken cancellationToken);

    Task<IReadOnlyList<WarehouseOrderItemProgress>>
        GetItemProgressAsync(
            int orderId,
            CancellationToken cancellationToken);

    Task<DateTime?> GetLastPickedAtUtcAsync(
        int orderId,
        CancellationToken cancellationToken);


}
