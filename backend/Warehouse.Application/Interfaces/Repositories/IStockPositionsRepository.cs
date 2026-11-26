
using Warehouse.Application.DTOs.Stock;

namespace Warehouse.Application.Interfaces.Repositories;

public interface IStockPositionsRepository
{
    Task<(IReadOnlyList<StockPositionDto> Items, int TotalCount)>
        GetPageAsync(
            StockPositionQuery query,
            CancellationToken cancellationToken);
}
