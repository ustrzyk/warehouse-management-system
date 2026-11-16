
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Warehouse.Application.DTOs.LogisticUnits;
using Warehouse.Application.Exceptions;
using Warehouse.Application.Interfaces.Repositories;
using Warehouse.Domain.Entities;
using Warehouse.Infrastructure.Persistence;
using Warehouse.Application.DTOs.LogisticUnitMovements;


namespace Warehouse.Infrastructure.Repositories;

public class LogisticUnitRepository : ILogisticUnitRepository
{
    private readonly WarehouseDbContext _context;

    public LogisticUnitRepository(WarehouseDbContext context)
    {
        _context = context;
    }

    public async Task<(
        IReadOnlyList<LogisticUnit> Items,
        int TotalCount)> GetPageAsync(
        LogisticUnitListQuery query,
        CancellationToken cancellationToken)
    {
        IQueryable<LogisticUnit> units =
            _context.LogisticUnits
                .AsNoTracking()
                .Include(x => x.StorageLocation);

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var search = query.Search.Trim();

            units = units.Where(x => x.Code.Contains(search));
        }

        var totalCount = await units
            .CountAsync(cancellationToken);

        var items = await units
            .OrderBy(x => x.Code)
            .ThenBy(x => x.Id)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .ToListAsync(cancellationToken);

        return (items, totalCount);
    }

    public Task<LogisticUnit?> GetByIdAsync(
        int id,
        CancellationToken cancellationToken)
    {
        return _context.LogisticUnits
            .AsNoTracking()
            .Include(x => x.StorageLocation)
            .FirstOrDefaultAsync(
                x => x.Id == id,
                cancellationToken);
    }

    public Task<bool> CodeExistsAsync(
        string code,
        CancellationToken cancellationToken)
    {
        return _context.LogisticUnits
            .AsNoTracking()
            .AnyAsync(
                x => x.Code == code,
                cancellationToken);
    }

    public async Task AddAsync(
        LogisticUnit unit,
        CancellationToken cancellationToken)
    {
        await _context.LogisticUnits.AddAsync(
            unit, cancellationToken);
    }

    public async Task SaveChangesAsync(
        CancellationToken cancellationToken)
    {
        try
        {
            await _context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException ex)
        {
            throw new ConcurrentLogisticUnitModificationException(ex);
        }
        catch (DbUpdateException ex)
            when (ex.InnerException is SqlException
            { Number: 2601 or 2627 })
        {
            throw new DuplicateLogisticUnitCodeException(ex);
        }
    }


    public Task<LogisticUnit?> GetTrackedByIdAsync(
        int id,
        CancellationToken cancellationToken)
    {
        return _context.LogisticUnits
            .Include(x => x.StorageLocation)
            .FirstOrDefaultAsync(
                x => x.Id == id,
                cancellationToken);
    }


    public async Task<StorageLocation?> GetValidMoveDestinationAsync(
        int fromStorageLocationId,
        int toStorageLocationId,
        CancellationToken cancellationToken)
    {
        var warehouseId = await _context.StorageLocations
            .AsNoTracking()
            .Where(x => x.Id == fromStorageLocationId)
            .Select(x => (int?)x.WarehouseZone.WarehouseId)
            .FirstOrDefaultAsync(cancellationToken);

        if (!warehouseId.HasValue)
            return null;

        return await _context.StorageLocations
            .AsNoTracking()
            .FirstOrDefaultAsync(
                x => x.Id == toStorageLocationId
                     && x.IsActive
                     && x.WarehouseZone.IsActive
                     && x.WarehouseZone.Warehouse.IsActive
                     && x.WarehouseZone.WarehouseId
                        == warehouseId.Value,
                cancellationToken);
    }


    public async Task AddMovementAsync(
        LogisticUnitMovement movement,
        CancellationToken cancellationToken)
    {
        await _context.LogisticUnitMovements.AddAsync(
            movement, cancellationToken);
    }


    public async Task<(
        IReadOnlyList<LogisticUnitMovement> Items,
        int TotalCount)> GetMovementsPageAsync(
        int logisticUnitId,
        LogisticUnitMovementListQuery query,
        CancellationToken cancellationToken)
    {
        IQueryable<LogisticUnitMovement> movements =
            _context.LogisticUnitMovements
                .AsNoTracking()
                .Where(x => x.LogisticUnitId == logisticUnitId);

        var totalCount = await movements.CountAsync(
            cancellationToken);

        var items = await movements
            .Include(x => x.LogisticUnit)
            .OrderByDescending(x => x.MovedAtUtc)
            .ThenByDescending(x => x.Id)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .ToListAsync(cancellationToken);

        return (items, totalCount);
    }

    public Task<LogisticUnitMovement?> GetMovementByIdAsync(
        int logisticUnitId,
        int movementId,
        CancellationToken cancellationToken)
    {
        return _context.LogisticUnitMovements
            .AsNoTracking()
            .Include(x => x.LogisticUnit)
            .FirstOrDefaultAsync(
                x => x.LogisticUnitId == logisticUnitId
                     && x.Id == movementId,
                cancellationToken);
    }

    public Task<bool> LocationIsActiveAsync(
        int storageLocationId,
        CancellationToken cancellationToken)
    {
        return _context.StorageLocations
            .AsNoTracking()
            .AnyAsync(
                x => x.Id == storageLocationId
                     && x.IsActive
                     && x.WarehouseZone.IsActive
                     && x.WarehouseZone.Warehouse.IsActive,
                cancellationToken);
    }

    public Task<bool> HasItemsAsync(
        int logisticUnitId,
        CancellationToken cancellationToken)
    {
        return _context.LogisticUnitItems
            .AsNoTracking()
            .AnyAsync(
                x => x.LogisticUnitId == logisticUnitId,
                cancellationToken);
    }

    public async Task<bool> LocationIsAllowedForUnitAsync(
        int logisticUnitId,
        int storageLocationId,
        CancellationToken cancellationToken)
    {
        var receiptWarehouseId = await _context.GoodsReceipts
            .AsNoTracking()
            .Where(x => x.LogisticUnitId == logisticUnitId)
            .Select(x => (int?)x.WarehouseId)
            .FirstOrDefaultAsync(cancellationToken);

        return await _context.StorageLocations
            .AsNoTracking()
            .AnyAsync(
                x => x.Id == storageLocationId
                     && x.IsActive
                     && x.WarehouseZone.IsActive
                     && x.WarehouseZone.Warehouse.IsActive
                     && (!receiptWarehouseId.HasValue ||
                         x.WarehouseZone.WarehouseId ==
                         receiptWarehouseId.Value),
                cancellationToken);
    }


}
