
using Warehouse.Application.DTOs.Common;
using Warehouse.Application.DTOs.GoodsReceipts;

namespace Warehouse.Application.Interfaces.Services;

public interface IGoodsReceiptService
{
    Task<PagedResult<GoodsReceiptDto>> GetPageAsync(
        GoodsReceiptListQuery query,
        CancellationToken cancellationToken);

    Task<GoodsReceiptDto?> GetByIdAsync(
        int id,
        CancellationToken cancellationToken);

    Task<GoodsReceiptDto> CreateAsync(
        CreateGoodsReceiptRequest request,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<GoodsReceiptItemDto>?> GetItemsAsync(
        int receiptId,
        CancellationToken cancellationToken);

    Task<GoodsReceiptItemDto?> AddItemAsync(
        int receiptId,
        AddGoodsReceiptItemRequest request,
        CancellationToken cancellationToken);
}
