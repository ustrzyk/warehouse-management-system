
using Warehouse.Application.DTOs.Stock;

namespace Warehouse.Application.Interfaces.Repositories;

public interface IStockAvailabilityRepository
{
    Task<(
        IReadOnlyList<StockAvailabilityDto> Items,
        int TotalCount)> GetPageAsync(
            StockAvailabilityQuery query,
            CancellationToken cancellationToken);
}
