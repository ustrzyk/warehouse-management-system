
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Warehouse.Application.DTOs.WarehouseZones;
using Warehouse.Application.Exceptions;
using Warehouse.Application.Interfaces.Repositories;
using Warehouse.Domain.Entities;
using Warehouse.Infrastructure.Persistence;

namespace Warehouse.Infrastructure.Repositories;

public class WarehouseZoneRepository : IWarehouseZoneRepository
{
    private readonly WarehouseDbContext _context;

    public WarehouseZoneRepository(WarehouseDbContext context)
    {
        _context = context;
    }

    public async Task<(
        IReadOnlyList<WarehouseZone> Items,
        int TotalCount)> GetPageAsync(
        WarehouseZoneListQuery query,
        CancellationToken cancellationToken)
    {
        IQueryable<WarehouseZone> zones =
            _context.WarehouseZones
                .AsNoTracking()
                .Include(x => x.Warehouse);

        if (query.WarehouseId.HasValue)
        {
            var warehouseId = query.WarehouseId.Value;

            zones = zones.Where(
                x => x.WarehouseId == warehouseId);
        }

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var search = query.Search.Trim();

            zones = zones.Where(x =>
                x.Code.Contains(search) ||
                x.Name.Contains(search));
        }

        if (query.IsActive.HasValue)
        {
            var isActive = query.IsActive.Value;

            zones = zones.Where(
                x => x.IsActive == isActive);
        }

        var totalCount = await zones.CountAsync(
            cancellationToken);

        var items = await zones
            .OrderBy(x => x.Warehouse.Code)
            .ThenBy(x => x.Code)
            .ThenBy(x => x.Id)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .ToListAsync(cancellationToken);

        return (items, totalCount);
    }

    public Task<WarehouseZone?> GetByIdAsync(
        int id,
        CancellationToken cancellationToken)
    {
        return _context.WarehouseZones
            .AsNoTracking()
            .Include(x => x.Warehouse)
            .FirstOrDefaultAsync(
                x => x.Id == id,
                cancellationToken);
    }

    public Task<WarehouseZone?> GetTrackedByIdAsync(
        int id,
        CancellationToken cancellationToken)
    {
        return _context.WarehouseZones
            .Include(x => x.Warehouse)
            .FirstOrDefaultAsync(
                x => x.Id == id,
                cancellationToken);
    }

    public Task<bool> WarehouseIsActiveAsync(
        int warehouseId,
        CancellationToken cancellationToken)
    {
        return _context.Warehouses
            .AsNoTracking()
            .AnyAsync(
                x => x.Id == warehouseId && x.IsActive,
                cancellationToken);
    }

    public Task<bool> CodeExistsAsync(
        int warehouseId,
        string code,
        int? excludeId,
        CancellationToken cancellationToken)
    {
        var zones = _context.WarehouseZones
            .AsNoTracking()
            .Where(x =>
                x.WarehouseId == warehouseId &&
                x.Code == code);

        if (excludeId.HasValue)
        {
            zones = zones.Where(
                x => x.Id != excludeId.Value);
        }

        return zones.AnyAsync(cancellationToken);
    }

    public Task<bool> HasLocatedLogisticUnitsAsync(
        int zoneId,
        CancellationToken cancellationToken)
    {
        return _context.LogisticUnits
            .AsNoTracking()
            .AnyAsync(
                x => x.StorageLocation != null &&
                     x.StorageLocation.WarehouseZoneId == zoneId,
                cancellationToken);
    }

    public async Task AddAsync(
        WarehouseZone zone,
        CancellationToken cancellationToken)
    {
        await _context.WarehouseZones.AddAsync(
            zone, cancellationToken);
    }

    public async Task SaveChangesAsync(
        CancellationToken cancellationToken)
    {
        try
        {
            await _context.SaveChangesAsync(
                cancellationToken);
        }
        catch (DbUpdateException ex)
            when (ex.InnerException is SqlException
            { Number: 2601 or 2627 })
        {
            throw new DuplicateWarehouseZoneCodeException(ex);
        }
    }
}
