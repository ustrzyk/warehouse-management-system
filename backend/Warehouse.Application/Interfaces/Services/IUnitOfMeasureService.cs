using Warehouse.Application.DTOs.Common;
using Warehouse.Application.DTOs.UnitsOfMeasure;

namespace Warehouse.Application.Interfaces.Services;

public interface IUnitOfMeasureService
{
    Task<PagedResult<UnitOfMeasureDto>> GetPageAsync(
        UnitOfMeasureListQuery query,
        CancellationToken cancellationToken);

    Task<UnitOfMeasureDto?> GetByIdAsync(
        int id,
        CancellationToken cancellationToken);

    Task<UnitOfMeasureDto> CreateAsync(
        CreateUnitOfMeasureRequest request,
        CancellationToken cancellationToken);

    Task<UnitOfMeasureDto?> UpdateAsync(
        int id,
        UpdateUnitOfMeasureRequest request,
        CancellationToken cancellationToken);

    Task<bool> DeactivateAsync(
        int id,
        CancellationToken cancellationToken);
}