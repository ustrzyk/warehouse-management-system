
using Warehouse.Application.DTOs.Common;
using Warehouse.Application.DTOs.Stock;
using Warehouse.Application.Interfaces.Repositories;
using Warehouse.Application.Interfaces.Services;

namespace Warehouse.Application.Services;

public class StockAvailabilityService
    : IStockAvailabilityService
{
    private readonly IStockAvailabilityRepository _repository;

    public StockAvailabilityService(
        IStockAvailabilityRepository repository)
    {
        _repository = repository;
    }

    public async Task<PagedResult<StockAvailabilityDto>> GetPageAsync(
        StockAvailabilityQuery query,
        CancellationToken cancellationToken)
    {
        var (items, totalCount) =
            await _repository.GetPageAsync(
                query, cancellationToken);

        return new PagedResult<StockAvailabilityDto>(
            items,
            totalCount,
            query.Page,
            query.PageSize);
    }
}
