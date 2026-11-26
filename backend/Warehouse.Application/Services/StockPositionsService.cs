
using Warehouse.Application.DTOs.Common;
using Warehouse.Application.DTOs.Stock;
using Warehouse.Application.Interfaces.Repositories;
using Warehouse.Application.Interfaces.Services;

namespace Warehouse.Application.Services;

public class StockPositionsService : IStockPositionsService
{
    private readonly IStockPositionsRepository _repository;

    public StockPositionsService(
        IStockPositionsRepository repository)
    {
        _repository = repository;
    }

    public async Task<PagedResult<StockPositionDto>> GetPageAsync(
        StockPositionQuery query,
        CancellationToken cancellationToken)
    {
        var (items, totalCount) =
            await _repository.GetPageAsync(
                query, cancellationToken);

        return new PagedResult<StockPositionDto>(
            items,
            totalCount,
            query.Page,
            query.PageSize);
    }
}
