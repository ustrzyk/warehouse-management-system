
using Microsoft.EntityFrameworkCore;
using Warehouse.Application.DTOs.Stock;
using Warehouse.Application.Interfaces.Repositories;
using Warehouse.Domain.Entities;
using Warehouse.Domain.Enums;
using Warehouse.Infrastructure.Persistence;

namespace Warehouse.Infrastructure.Repositories;

public class StockPositionsRepository : IStockPositionsRepository
{
    private readonly WarehouseDbContext _context;

    public StockPositionsRepository(WarehouseDbContext context)
    {
        _context = context;
    }

    public async Task<(
        IReadOnlyList<StockPositionDto> Items,
        int TotalCount)> GetPageAsync(
        StockPositionQuery filter,
        CancellationToken cancellationToken)
    {
        // Fizyczny stan magazynowy:
        // tylko dodatnie ilości na odłożonych paletach
        // w aktywnych lokalizacjach i magazynach.
        IQueryable<LogisticUnitItem> query =
            _context.LogisticUnitItems
                .AsNoTracking()
                .Where(x =>
                    x.Quantity > 0m &&
                    x.LogisticUnit.Status ==
                        LogisticUnitStatus.Stored &&
                    x.LogisticUnit.StorageLocation != null &&
                    x.LogisticUnit.StorageLocation.IsActive &&
                    x.LogisticUnit.StorageLocation
                        .WarehouseZone.IsActive &&
                    x.LogisticUnit.StorageLocation
                        .WarehouseZone.Warehouse.IsActive);

        if (filter.WarehouseId.HasValue)
        {
            var warehouseId = filter.WarehouseId.Value;

            query = query.Where(x =>
                x.LogisticUnit.StorageLocation!
                    .WarehouseZone.WarehouseId == warehouseId);
        }

        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            var search = filter.Search.Trim();

            query = query.Where(x =>
                x.Product.Code.Contains(search) ||
                x.Product.Name.Contains(search) ||
                x.LogisticUnit.Code.Contains(search));
        }

        if (!string.IsNullOrWhiteSpace(filter.BatchNumber))
        {
            var batch = filter.BatchNumber.Trim();

            query = query.Where(x =>
                x.ProductBatch != null &&
                x.ProductBatch.BatchNumber.Contains(batch));
        }

        if (!string.IsNullOrWhiteSpace(filter.LocationCode))
        {
            var location = filter.LocationCode.Trim();

            query = query.Where(x =>
                x.LogisticUnit.StorageLocation != null &&
                x.LogisticUnit.StorageLocation.Code
                    .Contains(location));
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var rows = await query
            .OrderBy(x =>
                x.LogisticUnit.StorageLocation!
                    .WarehouseZone.Warehouse.Code)
            .ThenBy(x => x.Product.Code)
            .ThenBy(x => x.LogisticUnit.Code)
            .ThenBy(x => x.Id)
            .Skip((filter.Page - 1) * filter.PageSize)
            .Take(filter.PageSize)
            .Select(x => new StockPositionDto(
                x.LogisticUnit.StorageLocation!
                    .WarehouseZone.WarehouseId,
                x.LogisticUnit.StorageLocation!
                    .WarehouseZone.Warehouse.Code,
                x.LogisticUnit.StorageLocation!
                    .WarehouseZone.Code,
                x.LogisticUnit.StorageLocation!.Code,
                x.LogisticUnitId,
                x.LogisticUnit.Code,
                x.ProductId,
                x.Product.Code,
                x.Product.Name,
                x.Product.UnitOfMeasure.Symbol,
                x.ProductBatchId,
                x.ProductBatch != null
                    ? x.ProductBatch.BatchNumber
                    : null,
                x.ProductBatch != null
                    ? x.ProductBatch.ExpirationDate
                    : null,
                x.Quantity
            ))
            .ToListAsync(cancellationToken);

        return (rows, totalCount);
    }
}
