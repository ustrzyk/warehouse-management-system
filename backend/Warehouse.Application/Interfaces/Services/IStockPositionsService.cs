using Warehouse.Application.DTOs.Common;
using Warehouse.Application.DTOs.Stock;

namespace Warehouse.Application.Interfaces.Services;

public interface IStockPositionsService
{
    Task<PagedResult<StockPositionDto>> GetPageAsync(
        StockPositionQuery query,
        CancellationToken cancellationToken);
}