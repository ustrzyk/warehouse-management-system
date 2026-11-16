
using Warehouse.Application.DTOs.Common;
using Warehouse.Application.DTOs.LogisticUnits;
using Warehouse.Application.Exceptions;
using Warehouse.Application.Interfaces.Repositories;
using Warehouse.Application.Interfaces.Services;
using Warehouse.Domain.Entities;
using Warehouse.Domain.Enums;
using Warehouse.Application.DTOs.LogisticUnitMovements;


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

        if (!await _repository.LocationIsAllowedForUnitAsync(
            id, request.StorageLocationId, cancellationToken))
        {
            throw new InvalidLogisticUnitException(
                "Lokalizacja nie jest aktywna lub nalezy do niewlasciwego magazynu.");
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


    public async Task<LogisticUnitMovementDto?> MoveAsync(
        int id,
        MoveLogisticUnitRequest request,
        CancellationToken cancellationToken)
    {
        var unit = await _repository.GetTrackedByIdAsync(
            id, cancellationToken);

        if (unit is null)
            return null;

        if (unit.Status != LogisticUnitStatus.Stored ||
            !unit.StorageLocationId.HasValue)
        {
            throw new InvalidLogisticUnitException(
                "Przesuwac mozna tylko palety Stored z lokalizacja.");
        }

        var fromId = unit.StorageLocationId.Value;

        if (fromId == request.ToStorageLocationId)
        {
            throw new InvalidLogisticUnitException(
                "Lokalizacja docelowa musi byc inna niz aktualna.");
        }

        var destination = await _repository.GetValidMoveDestinationAsync(
            fromId,
            request.ToStorageLocationId,
            cancellationToken);

        if (destination is null)
        {
            throw new InvalidLogisticUnitException(
                "Lokalizacja docelowa jest nieaktywna, " +
                "nie istnieje lub nalezy do innego magazynu.");
        }

        var fromCode = unit.StorageLocation?.Code
            ?? throw new InvalidOperationException(
                "Source storage location could not be loaded.");

        var movement = new LogisticUnitMovement
        {
            LogisticUnitId = unit.Id,

            FromStorageLocationId = fromId,
            FromLocationCode = fromCode,

            ToStorageLocationId = destination.Id,
            ToLocationCode = destination.Code,

            MovedAtUtc = DateTime.UtcNow,

            Note = string.IsNullOrWhiteSpace(request.Note)
                ? null
                : request.Note.Trim()
        };

        unit.StorageLocationId = destination.Id;

        await _repository.AddMovementAsync(
            movement, cancellationToken);

        await _repository.SaveChangesAsync(
            cancellationToken);

        var created = await _repository.GetMovementByIdAsync(
            id, movement.Id, cancellationToken);

        return created is null
            ? throw new InvalidOperationException(
                "Created movement could not be loaded.")
            : MapMovementToDto(created);
    }

    public async Task<PagedResult<LogisticUnitMovementDto>?>
        GetMovementsPageAsync(
            int id,
            LogisticUnitMovementListQuery query,
            CancellationToken cancellationToken)
    {
        var unit = await _repository.GetByIdAsync(
            id, cancellationToken);

        if (unit is null)
            return null;

        var (items, totalCount) =
            await _repository.GetMovementsPageAsync(
                id, query, cancellationToken);

        return new PagedResult<LogisticUnitMovementDto>(
            items.Select(MapMovementToDto).ToList(),
            totalCount,
            query.Page,
            query.PageSize);
    }

    public async Task<LogisticUnitMovementDto?> GetMovementByIdAsync(
        int id,
        int movementId,
        CancellationToken cancellationToken)
    {
        var movement = await _repository.GetMovementByIdAsync(
            id, movementId, cancellationToken);

        return movement is null
            ? null
            : MapMovementToDto(movement);
    }

    private static LogisticUnitMovementDto MapMovementToDto(
        LogisticUnitMovement movement)
    {
        return new LogisticUnitMovementDto(
            movement.Id,
            movement.LogisticUnitId,
            movement.LogisticUnit.Code,
            movement.FromStorageLocationId,
            movement.FromLocationCode,
            movement.ToStorageLocationId,
            movement.ToLocationCode,
            movement.MovedAtUtc,
            movement.Note);
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
