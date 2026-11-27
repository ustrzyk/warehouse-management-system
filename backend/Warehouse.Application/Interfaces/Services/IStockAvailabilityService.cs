
using Warehouse.Application.DTOs.Common;
using Warehouse.Application.DTOs.Stock;

namespace Warehouse.Application.Interfaces.Services;

public interface IStockAvailabilityService
{
    Task<PagedResult<StockAvailabilityDto>> GetPageAsync(
        StockAvailabilityQuery query,
        CancellationToken cancellationToken);
}
