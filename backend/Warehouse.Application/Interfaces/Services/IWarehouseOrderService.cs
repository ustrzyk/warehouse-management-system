
using Warehouse.Application.DTOs.Common;
using Warehouse.Application.DTOs.WarehouseOrders;

namespace Warehouse.Application.Interfaces.Services;

public interface IWarehouseOrderService
{
    Task<PagedResult<WarehouseOrderDto>> GetPageAsync(
        WarehouseOrderListQuery query,
        CancellationToken cancellationToken);

    Task<WarehouseOrderDto?> GetByIdAsync(
        int id,
        CancellationToken cancellationToken);

    Task<WarehouseOrderDto> CreateAsync(
        CreateWarehouseOrderRequest request,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<WarehouseOrderItemDto>?> GetItemsAsync(
        int orderId,
        CancellationToken cancellationToken);

    Task<WarehouseOrderItemDto?> AddItemAsync(
        int orderId,
        AddWarehouseOrderItemRequest request,
        CancellationToken cancellationToken);

    Task<WarehouseOrderDto?> ReleaseAsync(
        int id,
        CancellationToken cancellationToken);

}
