
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Warehouse.Application.DTOs.Warehouses;
using Warehouse.Application.Exceptions;
using Warehouse.Application.Interfaces.Repositories;
using Warehouse.Infrastructure.Persistence;
using WarehouseEntity = global::Warehouse.Domain.Entities.Warehouse;

namespace Warehouse.Infrastructure.Repositories;

public class WarehouseRepository : IWarehouseRepository
{
    private readonly WarehouseDbContext _context;

    public WarehouseRepository(WarehouseDbContext context)
    {
        _context = context;
    }

    public async Task<(
        IReadOnlyList<WarehouseEntity> Items,
        int TotalCount)> GetPageAsync(
        WarehouseListQuery query,
        CancellationToken cancellationToken)
    {
        IQueryable<WarehouseEntity> warehouses =
            _context.Warehouses.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var search = query.Search.Trim();

            warehouses = warehouses.Where(x =>
                x.Code.Contains(search) ||
                x.Name.Contains(search));
        }

        if (query.IsActive.HasValue)
        {
            var active = query.IsActive.Value;
            warehouses = warehouses.Where(
                x => x.IsActive == active);
        }

        var totalCount = await warehouses.CountAsync(
            cancellationToken);

        var items = await warehouses
            .OrderBy(x => x.Code)
            .ThenBy(x => x.Id)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .ToListAsync(cancellationToken);

        return (items, totalCount);
    }

    public Task<WarehouseEntity?> GetByIdAsync(
        int id,
        CancellationToken cancellationToken)
    {
        return _context.Warehouses
            .AsNoTracking()
            .FirstOrDefaultAsync(
                x => x.Id == id,
                cancellationToken);
    }

    public Task<WarehouseEntity?> GetTrackedByIdAsync(
        int id,
        CancellationToken cancellationToken)
    {
        return _context.Warehouses
            .FirstOrDefaultAsync(
                x => x.Id == id,
                cancellationToken);
    }

    public Task<bool> CodeExistsAsync(
        string code,
        int? excludeId,
        CancellationToken cancellationToken)
    {
        var query = _context.Warehouses
            .AsNoTracking()
            .Where(x => x.Code == code);

        if (excludeId.HasValue)
        {
            query = query.Where(
                x => x.Id != excludeId.Value);
        }

        return query.AnyAsync(cancellationToken);
    }

    public Task<bool> HasLocatedLogisticUnitsAsync(
        int warehouseId,
        CancellationToken cancellationToken)
    {
        return _context.LogisticUnits
            .AsNoTracking()
            .AnyAsync(
                x => x.StorageLocation != null &&
                     x.StorageLocation.WarehouseZone.WarehouseId
                         == warehouseId,
                cancellationToken);
    }

    public async Task AddAsync(
        WarehouseEntity warehouse,
        CancellationToken cancellationToken)
    {
        await _context.Warehouses.AddAsync(
            warehouse, cancellationToken);
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
            throw new DuplicateWarehouseCodeException(ex);
        }
    }
}
