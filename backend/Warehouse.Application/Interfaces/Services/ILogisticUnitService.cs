
using Warehouse.Application.DTOs.Common;
using Warehouse.Application.DTOs.LogisticUnits;

namespace Warehouse.Application.Interfaces.Services;

public interface ILogisticUnitService
{
    Task<PagedResult<LogisticUnitDto>> GetPageAsync(
        LogisticUnitListQuery query,
        CancellationToken cancellationToken);

    Task<LogisticUnitDto?> GetByIdAsync(
        int id,
        CancellationToken cancellationToken);

    Task<LogisticUnitDto> CreateAsync(
        CreateLogisticUnitRequest request,
        CancellationToken cancellationToken);

    Task<LogisticUnitDto?> StoreAsync(
        int id,
        StoreLogisticUnitRequest request,
        CancellationToken cancellationToken);

}
