
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Warehouse.Application.DTOs.StorageLocations;
using Warehouse.Application.Exceptions;
using Warehouse.Application.Interfaces.Repositories;
using Warehouse.Domain.Entities;
using Warehouse.Infrastructure.Persistence;

namespace Warehouse.Infrastructure.Repositories;

public class StorageLocationRepository
    : IStorageLocationRepository
{
    private readonly WarehouseDbContext _context;

    public StorageLocationRepository(
        WarehouseDbContext context)
    {
        _context = context;
    }

    public async Task<(
        IReadOnlyList<StorageLocation> Items,
        int TotalCount)> GetPageAsync(
        StorageLocationListQuery query,
        CancellationToken cancellationToken)
    {
        IQueryable<StorageLocation> locations =
            _context.StorageLocations
                .AsNoTracking()
                .Include(x => x.WarehouseZone)
                    .ThenInclude(x => x.Warehouse);

        if (query.WarehouseId.HasValue)
        {
            var warehouseId = query.WarehouseId.Value;

            locations = locations.Where(
                x => x.WarehouseZone.WarehouseId == warehouseId);
        }

        if (query.WarehouseZoneId.HasValue)
        {
            var zoneId = query.WarehouseZoneId.Value;

            locations = locations.Where(
                x => x.WarehouseZoneId == zoneId);
        }

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var search = query.Search.Trim();

            locations = locations.Where(x =>
                x.Code.Contains(search) ||
                (x.Description != null &&
                 x.Description.Contains(search)));
        }

        if (query.IsActive.HasValue)
        {
            var active = query.IsActive.Value;

            locations = locations.Where(
                x => x.IsActive == active);
        }

        var totalCount = await locations.CountAsync(
            cancellationToken);

        var items = await locations
            .OrderBy(x => x.WarehouseZone.Warehouse.Code)
            .ThenBy(x => x.WarehouseZone.Code)
            .ThenBy(x => x.Code)
            .ThenBy(x => x.Id)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .ToListAsync(cancellationToken);

        return (items, totalCount);
    }

    public Task<StorageLocation?> GetByIdAsync(
        int id,
        CancellationToken cancellationToken)
    {
        return _context.StorageLocations
            .AsNoTracking()
            .Include(x => x.WarehouseZone)
                .ThenInclude(x => x.Warehouse)
            .FirstOrDefaultAsync(
                x => x.Id == id,
                cancellationToken);
    }

    public Task<StorageLocation?> GetTrackedByIdAsync(
        int id,
        CancellationToken cancellationToken)
    {
        return _context.StorageLocations
            .Include(x => x.WarehouseZone)
                .ThenInclude(x => x.Warehouse)
            .FirstOrDefaultAsync(
                x => x.Id == id,
                cancellationToken);
    }

    public Task<bool> ZoneAndWarehouseAreActiveAsync(
        int warehouseZoneId,
        CancellationToken cancellationToken)
    {
        return _context.WarehouseZones
            .AsNoTracking()
            .AnyAsync(
                x => x.Id == warehouseZoneId
                     && x.IsActive
                     && x.Warehouse.IsActive,
                cancellationToken);
    }

    public Task<bool> CodeExistsAsync(
        int warehouseZoneId,
        string code,
        int? excludeId,
        CancellationToken cancellationToken)
    {
        var query = _context.StorageLocations
            .AsNoTracking()
            .Where(x =>
                x.WarehouseZoneId == warehouseZoneId &&
                x.Code == code);

        if (excludeId.HasValue)
        {
            query = query.Where(
                x => x.Id != excludeId.Value);
        }

        return query.AnyAsync(cancellationToken);
    }

    public Task<bool> HasAssignedLogisticUnitsAsync(
        int storageLocationId,
        CancellationToken cancellationToken)
    {
        return _context.LogisticUnits
            .AsNoTracking()
            .AnyAsync(
                x => x.StorageLocationId == storageLocationId,
                cancellationToken);
    }

    public async Task AddAsync(
        StorageLocation location,
        CancellationToken cancellationToken)
    {
        await _context.StorageLocations.AddAsync(
            location, cancellationToken);
    }

    public async Task SaveChangesAsync(
        CancellationToken cancellationToken)
    {
        try
        {
            await _context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex)
            when (ex.InnerException is SqlException
            { Number: 2601 or 2627 })
        {
            throw new DuplicateStorageLocationCodeException(ex);
        }
    }
}
