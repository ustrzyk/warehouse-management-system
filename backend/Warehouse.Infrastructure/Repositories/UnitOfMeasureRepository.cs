using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Warehouse.Application.DTOs.UnitsOfMeasure;
using Warehouse.Application.Exceptions;
using Warehouse.Application.Interfaces.Repositories;
using Warehouse.Domain.Entities;
using Warehouse.Infrastructure.Persistence;

namespace Warehouse.Infrastructure.Repositories;

public class UnitOfMeasureRepository : IUnitOfMeasureRepository
{
    private readonly WarehouseDbContext _context;

    public UnitOfMeasureRepository(WarehouseDbContext context)
    {
        _context = context;
    }

    public async Task<(
        IReadOnlyList<UnitOfMeasure> Items,
        int TotalCount)> GetPageAsync(
        UnitOfMeasureListQuery query,
        CancellationToken cancellationToken)
    {
        IQueryable<UnitOfMeasure> units =
            _context.UnitsOfMeasure.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var search = query.Search.Trim();

            units = units.Where(x =>
                x.Code.Contains(search) ||
                x.Name.Contains(search) ||
                x.Symbol.Contains(search));
        }

        if (query.IsActive.HasValue)
        {
            var isActive = query.IsActive.Value;

            units = units.Where(x => x.IsActive == isActive);
        }

        var totalCount = await units.CountAsync(cancellationToken);

        units = (query.SortBy.ToLowerInvariant(),
                 query.Descending) switch
        {
            ("code", true) =>
                units.OrderByDescending(x => x.Code)
                    .ThenBy(x => x.Id),

            ("code", false) =>
                units.OrderBy(x => x.Code)
                    .ThenBy(x => x.Id),

            ("symbol", true) =>
                units.OrderByDescending(x => x.Symbol)
                    .ThenBy(x => x.Id),

            ("symbol", false) =>
                units.OrderBy(x => x.Symbol)
                    .ThenBy(x => x.Id),

            ("name", true) =>
                units.OrderByDescending(x => x.Name)
                    .ThenBy(x => x.Id),

            _ =>
                units.OrderBy(x => x.Name)
                    .ThenBy(x => x.Id)
        };

        var items = await units
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .ToListAsync(cancellationToken);

        return (items, totalCount);
    }

    public async Task<UnitOfMeasure?> GetByIdAsync(
        int id,
        CancellationToken cancellationToken)
    {
        return await _context.UnitsOfMeasure
            .AsNoTracking()
            .FirstOrDefaultAsync(
                x => x.Id == id, cancellationToken);
    }

    public async Task<UnitOfMeasure?> GetTrackedByIdAsync(
        int id,
        CancellationToken cancellationToken)
    {
        return await _context.UnitsOfMeasure
            .FirstOrDefaultAsync(
                x => x.Id == id, cancellationToken);
    }

    public async Task<bool> CodeExistsAsync(
        string code,
        int? excludeId,
        CancellationToken cancellationToken)
    {
        var query = _context.UnitsOfMeasure
            .AsNoTracking()
            .Where(x => x.Code == code);

        if (excludeId.HasValue)
        {
            query = query.Where(
                x => x.Id != excludeId.Value);
        }

        return await query.AnyAsync(cancellationToken);
    }

    public async Task AddAsync(
        UnitOfMeasure unit,
        CancellationToken cancellationToken)
    {
        await _context.UnitsOfMeasure.AddAsync(
            unit, cancellationToken);
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
            throw new DuplicateUnitOfMeasureCodeException(ex);
        }
    }
}