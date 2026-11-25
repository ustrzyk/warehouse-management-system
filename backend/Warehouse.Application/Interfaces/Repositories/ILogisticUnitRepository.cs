
using Warehouse.Application.DTOs.LogisticUnits;
using Warehouse.Domain.Entities;
using Warehouse.Application.DTOs.LogisticUnitMovements;


namespace Warehouse.Application.Interfaces.Repositories;

public interface ILogisticUnitRepository
{
    Task<(IReadOnlyList<LogisticUnit> Items, int TotalCount)>
        GetPageAsync(
            LogisticUnitListQuery query,
            CancellationToken cancellationToken);

    Task<LogisticUnit?> GetByIdAsync(
        int id,
        CancellationToken cancellationToken);

    Task<IReadOnlySet<int>> GetGoodsReceiptPalletIdsAsync(
        IReadOnlyCollection<int> palletIds,
        CancellationToken cancellationToken);

    Task<bool> CodeExistsAsync(
        string code,
        CancellationToken cancellationToken);

    Task AddAsync(
        LogisticUnit unit,
        CancellationToken cancellationToken);

    Task SaveChangesAsync(
        CancellationToken cancellationToken);

    Task<LogisticUnit?> GetTrackedByIdAsync(
        int id,
        CancellationToken cancellationToken);

    Task<bool> LocationIsActiveAsync(
        int storageLocationId,
        CancellationToken cancellationToken);

    Task<bool> HasItemsAsync(
        int logisticUnitId,
        CancellationToken cancellationToken);


    Task<StorageLocation?> GetValidMoveDestinationAsync(
        int fromStorageLocationId,
        int toStorageLocationId,
        CancellationToken cancellationToken);

    Task AddMovementAsync(
        LogisticUnitMovement movement,
        CancellationToken cancellationToken);

    Task<(IReadOnlyList<LogisticUnitMovement> Items, int TotalCount)>
        GetMovementsPageAsync(
            int logisticUnitId,
            LogisticUnitMovementListQuery query,
            CancellationToken cancellationToken);

    Task<LogisticUnitMovement?> GetMovementByIdAsync(
        int logisticUnitId,
        int movementId,
        CancellationToken cancellationToken);

    Task<bool> LocationIsAllowedForUnitAsync(
        int logisticUnitId,
        int storageLocationId,
        CancellationToken cancellationToken);

}
