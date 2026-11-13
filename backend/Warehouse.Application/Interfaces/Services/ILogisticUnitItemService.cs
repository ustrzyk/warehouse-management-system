
using Warehouse.Application.DTOs.LogisticUnitItems;

namespace Warehouse.Application.Interfaces.Services;

public interface ILogisticUnitItemService
{
    Task<IReadOnlyList<LogisticUnitItemDto>?> GetItemsAsync(
        int logisticUnitId,
        CancellationToken cancellationToken);

    Task<LogisticUnitItemDto?> GetByIdAsync(
        int logisticUnitId,
        int itemId,
        CancellationToken cancellationToken);

    Task<LogisticUnitItemDto?> CreateAsync(
        int logisticUnitId,
        CreateLogisticUnitItemRequest request,
        CancellationToken cancellationToken);
}
