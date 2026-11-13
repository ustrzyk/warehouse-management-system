
using Warehouse.Application.DTOs.LogisticUnits;
using Warehouse.Domain.Entities;

namespace Warehouse.Application.Interfaces.Repositories;

public interface ILogisticUnitRepository
{
    Task<(IReadOnlyList<LogisticUnit> Items, int TotalCount)>
        GetPageAsync(
            LogisticUnitListQuery query,
            CancellationToken cancellationToken);

    Task<LogisticUnit?> GetByIdAsync(
        int id,
        CancellationToken cancellationToken);

    Task<bool> CodeExistsAsync(
        string code,
        CancellationToken cancellationToken);

    Task AddAsync(
        LogisticUnit unit,
        CancellationToken cancellationToken);

    Task SaveChangesAsync(
        CancellationToken cancellationToken);
}
