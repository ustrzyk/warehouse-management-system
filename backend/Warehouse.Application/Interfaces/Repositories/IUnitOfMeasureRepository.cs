using Warehouse.Application.DTOs.UnitsOfMeasure;
using Warehouse.Domain.Entities;

namespace Warehouse.Application.Interfaces.Repositories;

public interface IUnitOfMeasureRepository
{
    Task<(IReadOnlyList<UnitOfMeasure> Items, int TotalCount)>
        GetPageAsync(
            UnitOfMeasureListQuery query,
            CancellationToken cancellationToken);

    Task<UnitOfMeasure?> GetByIdAsync(
        int id,
        CancellationToken cancellationToken);

    Task<UnitOfMeasure?> GetTrackedByIdAsync(
        int id,
        CancellationToken cancellationToken);

    Task<bool> CodeExistsAsync(
        string code,
        int? excludeId,
        CancellationToken cancellationToken);

    Task AddAsync(
        UnitOfMeasure unit,
        CancellationToken cancellationToken);

    Task SaveChangesAsync(
        CancellationToken cancellationToken);
}