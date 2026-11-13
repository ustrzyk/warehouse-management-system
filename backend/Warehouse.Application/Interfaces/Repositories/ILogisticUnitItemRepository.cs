
using Warehouse.Domain.Entities;

namespace Warehouse.Application.Interfaces.Repositories;

public interface ILogisticUnitItemRepository
{
    Task<LogisticUnit?> GetLogisticUnitAsync(
        int logisticUnitId,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<LogisticUnitItem>> GetItemsAsync(
        int logisticUnitId,
        CancellationToken cancellationToken);

    Task<LogisticUnitItem?> GetByIdAsync(
        int logisticUnitId,
        int itemId,
        CancellationToken cancellationToken);

    Task<bool> ProductIsActiveAsync(
        int productId,
        CancellationToken cancellationToken);

    Task<bool> BatchIsActiveForProductAsync(
        int productBatchId,
        int productId,
        CancellationToken cancellationToken);

    Task<bool> ItemExistsAsync(
        int logisticUnitId,
        int productId,
        int? productBatchId,
        CancellationToken cancellationToken);

    Task AddAsync(
        LogisticUnitItem item,
        CancellationToken cancellationToken);

    Task SaveChangesAsync(
        CancellationToken cancellationToken);
}
