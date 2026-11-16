
using Warehouse.Application.DTOs.Common;
using Warehouse.Application.DTOs.StorageLocations;
using Warehouse.Application.Exceptions;
using Warehouse.Application.Interfaces.Repositories;
using Warehouse.Application.Interfaces.Services;
using Warehouse.Domain.Entities;

namespace Warehouse.Application.Services;

public class StorageLocationService : IStorageLocationService
{
    private readonly IStorageLocationRepository _repository;

    public StorageLocationService(
        IStorageLocationRepository repository)
    {
        _repository = repository;
    }

    public async Task<PagedResult<StorageLocationDto>> GetPageAsync(
        StorageLocationListQuery query,
        CancellationToken cancellationToken)
    {
        var (items, totalCount) =
            await _repository.GetPageAsync(
                query, cancellationToken);

        return new PagedResult<StorageLocationDto>(
            items.Select(MapToDto).ToList(),
            totalCount,
            query.Page,
            query.PageSize);
    }

    public async Task<StorageLocationDto?> GetByIdAsync(
        int id,
        CancellationToken cancellationToken)
    {
        var location = await _repository.GetByIdAsync(
            id, cancellationToken);

        return location is null ? null : MapToDto(location);
    }

    public async Task<StorageLocationDto> CreateAsync(
        CreateStorageLocationRequest request,
        CancellationToken cancellationToken)
    {
        ValidateCode(request.Code);

        if (!await _repository.ZoneAndWarehouseAreActiveAsync(
            request.WarehouseZoneId, cancellationToken))
        {
            throw new InvalidStorageLocationException(
                "Strefa nie istnieje lub strefa albo magazyn jest nieaktywny.");
        }

        var code = request.Code.Trim().ToUpperInvariant();

        if (await _repository.CodeExistsAsync(
            request.WarehouseZoneId,
            code,
            null,
            cancellationToken))
        {
            throw new DuplicateStorageLocationCodeException();
        }

        var location = new StorageLocation
        {
            WarehouseZoneId = request.WarehouseZoneId,
            Code = code,
            Description = NormalizeDescription(request.Description),
            IsActive = true
        };

        await _repository.AddAsync(
            location, cancellationToken);

        await _repository.SaveChangesAsync(
            cancellationToken);

        var created = await _repository.GetByIdAsync(
            location.Id, cancellationToken);

        return created is null
            ? throw new InvalidOperationException(
                "Created storage location could not be loaded.")
            : MapToDto(created);
    }

    public async Task<StorageLocationDto?> UpdateAsync(
        int id,
        UpdateStorageLocationRequest request,
        CancellationToken cancellationToken)
    {
        var location = await _repository.GetTrackedByIdAsync(
            id, cancellationToken);

        if (location is null)
            return null;

        ValidateCode(request.Code);

        if (request.IsActive &&
            (!location.WarehouseZone.IsActive ||
             !location.WarehouseZone.Warehouse.IsActive))
        {
            throw new InvalidStorageLocationException(
                "Nie mozna aktywowac lokalizacji w nieaktywnej strefie lub magazynie.");
        }

        var code = request.Code.Trim().ToUpperInvariant();

        if (await _repository.CodeExistsAsync(
            location.WarehouseZoneId,
            code,
            id,
            cancellationToken))
        {
            throw new DuplicateStorageLocationCodeException();
        }

        if (location.IsActive && !request.IsActive)
        {
            await EnsureCanDeactivateAsync(
                id, cancellationToken);
        }

        location.Code = code;
        location.Description =
            NormalizeDescription(request.Description);
        location.IsActive = request.IsActive;

        await _repository.SaveChangesAsync(
            cancellationToken);

        return MapToDto(location);
    }

    public async Task<bool> DeactivateAsync(
        int id,
        CancellationToken cancellationToken)
    {
        var location = await _repository.GetTrackedByIdAsync(
            id, cancellationToken);

        if (location is null)
            return false;

        if (location.IsActive)
        {
            await EnsureCanDeactivateAsync(
                id, cancellationToken);

            location.IsActive = false;

            await _repository.SaveChangesAsync(
                cancellationToken);
        }

        return true;
    }

    private async Task EnsureCanDeactivateAsync(
        int id,
        CancellationToken cancellationToken)
    {
        if (await _repository.HasAssignedLogisticUnitsAsync(
            id, cancellationToken))
        {
            throw new InvalidStorageLocationException(
                "Nie mozna dezaktywowac lokalizacji z przypisanymi paletami.");
        }
    }

    private static void ValidateCode(string code)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            throw new InvalidStorageLocationException(
                "Kod lokalizacji nie moze byc pusty.");
        }
    }

    private static string? NormalizeDescription(
        string? description)
    {
        return string.IsNullOrWhiteSpace(description)
            ? null
            : description.Trim();
    }

    private static StorageLocationDto MapToDto(
        StorageLocation location)
    {
        return new StorageLocationDto(
            location.Id,
            location.WarehouseZoneId,
            location.WarehouseZone.Code,
            location.WarehouseZone.WarehouseId,
            location.WarehouseZone.Warehouse.Code,
            location.Code,
            location.Description,
            location.IsActive);
    }
}
