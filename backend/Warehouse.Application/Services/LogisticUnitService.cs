
using Warehouse.Application.DTOs.Common;
using Warehouse.Application.DTOs.LogisticUnits;
using Warehouse.Application.Exceptions;
using Warehouse.Application.Interfaces.Repositories;
using Warehouse.Application.Interfaces.Services;
using Warehouse.Domain.Entities;
using Warehouse.Domain.Enums;

namespace Warehouse.Application.Services;

public class LogisticUnitService : ILogisticUnitService
{
    private readonly ILogisticUnitRepository _repository;

    public LogisticUnitService(
        ILogisticUnitRepository repository)
    {
        _repository = repository;
    }

    public async Task<PagedResult<LogisticUnitDto>> GetPageAsync(
        LogisticUnitListQuery query,
        CancellationToken cancellationToken)
    {
        var (items, totalCount) =
            await _repository.GetPageAsync(
                query, cancellationToken);

        return new PagedResult<LogisticUnitDto>(
            items.Select(MapToDto).ToList(),
            totalCount,
            query.Page,
            query.PageSize);
    }

    public async Task<LogisticUnitDto?> GetByIdAsync(
        int id,
        CancellationToken cancellationToken)
    {
        var unit = await _repository.GetByIdAsync(
            id, cancellationToken);

        return unit is null ? null : MapToDto(unit);
    }

    public async Task<LogisticUnitDto> CreateAsync(
        CreateLogisticUnitRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Code))
        {
            throw new InvalidLogisticUnitException(
                "Numer LE nie może być pusty.");
        }

        var code = request.Code.Trim().ToUpperInvariant();

        if (await _repository.CodeExistsAsync(
            code, cancellationToken))
        {
            throw new DuplicateLogisticUnitCodeException();
        }

        var unit = new LogisticUnit
        {
            Code = code,
            Status = LogisticUnitStatus.Created,
            StorageLocationId = null,
            CreatedAtUtc = DateTime.UtcNow
        };

        await _repository.AddAsync(
            unit, cancellationToken);

        await _repository.SaveChangesAsync(
            cancellationToken);

        return MapToDto(unit);
    }


    public async Task<LogisticUnitDto?> StoreAsync(
        int id,
        StoreLogisticUnitRequest request,
        CancellationToken cancellationToken)
    {
        var unit = await _repository.GetTrackedByIdAsync(
            id, cancellationToken);

        if (unit is null)
            return null;

        if (unit.Status != LogisticUnitStatus.Created)
        {
            throw new InvalidLogisticUnitException(
                "Tylko paleta Created moze zostac odlozona.");
        }

        if (!await _repository.HasItemsAsync(
            id, cancellationToken))
        {
            throw new InvalidLogisticUnitException(
                "Nie mozna odlozyc pustej palety.");
        }

        if (!await _repository.LocationIsActiveAsync(
            request.StorageLocationId, cancellationToken))
        {
            throw new InvalidLogisticUnitException(
                "Lokalizacja nie istnieje lub nie jest aktywna.");
        }

        unit.StorageLocationId = request.StorageLocationId;
        unit.Status = LogisticUnitStatus.Stored;

        await _repository.SaveChangesAsync(
            cancellationToken);

        var updated = await _repository.GetByIdAsync(
            id, cancellationToken);

        return updated is null
            ? throw new InvalidOperationException(
                "Stored logistic unit could not be loaded.")
            : MapToDto(updated);
    }


    private static LogisticUnitDto MapToDto(
        LogisticUnit unit)
    {
        return new LogisticUnitDto(
            unit.Id,
            unit.Code,
            unit.Status.ToString(),
            unit.StorageLocationId,
            unit.StorageLocation?.Code,
            unit.CreatedAtUtc);
    }
}
