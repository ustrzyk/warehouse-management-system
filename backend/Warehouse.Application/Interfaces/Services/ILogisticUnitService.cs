
using Warehouse.Application.DTOs.Common;
using Warehouse.Application.DTOs.LogisticUnits;
using Warehouse.Application.DTOs.LogisticUnitMovements;


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

    Task<LogisticUnitMovementDto?> MoveAsync(
        int id,
        MoveLogisticUnitRequest request,
        CancellationToken cancellationToken);

    Task<PagedResult<LogisticUnitMovementDto>?> GetMovementsPageAsync(
        int id,
        LogisticUnitMovementListQuery query,
        CancellationToken cancellationToken);

    Task<LogisticUnitMovementDto?> GetMovementByIdAsync(
        int id,
        int movementId,
        CancellationToken cancellationToken);


}
