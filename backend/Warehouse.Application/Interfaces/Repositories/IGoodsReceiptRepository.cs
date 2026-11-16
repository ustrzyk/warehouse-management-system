
using Warehouse.Application.DTOs.GoodsReceipts;
using Warehouse.Domain.Entities;

namespace Warehouse.Application.Interfaces.Repositories;

public interface IGoodsReceiptRepository
{
    Task<(IReadOnlyList<GoodsReceipt> Items, int TotalCount)>
        GetPageAsync(
            GoodsReceiptListQuery query,
            CancellationToken cancellationToken);

    Task<GoodsReceipt?> GetByIdAsync(
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
        int receiptId,
        int productId,
        int? batchId,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<GoodsReceiptItem>> GetItemsAsync(
        int receiptId,
        CancellationToken cancellationToken);

    Task<GoodsReceiptItem?> GetItemByIdAsync(
        int receiptId,
        int itemId,
        CancellationToken cancellationToken);

    Task AddAsync(
        GoodsReceipt receipt,
        CancellationToken cancellationToken);

    Task AddItemAsync(
        GoodsReceiptItem item,
        CancellationToken cancellationToken);

    Task SaveChangesAsync(
        CancellationToken cancellationToken);
}
