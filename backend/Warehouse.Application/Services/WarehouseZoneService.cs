
using Warehouse.Application.DTOs.Common;
using Warehouse.Application.DTOs.WarehouseZones;
using Warehouse.Application.Exceptions;
using Warehouse.Application.Interfaces.Repositories;
using Warehouse.Application.Interfaces.Services;
using Warehouse.Domain.Entities;

namespace Warehouse.Application.Services;

public class WarehouseZoneService : IWarehouseZoneService
{
    private readonly IWarehouseZoneRepository _repository;

    public WarehouseZoneService(
        IWarehouseZoneRepository repository)
    {
        _repository = repository;
    }

    public async Task<PagedResult<WarehouseZoneDto>> GetPageAsync(
        WarehouseZoneListQuery query,
        CancellationToken cancellationToken)
    {
        var (items, totalCount) =
            await _repository.GetPageAsync(
                query, cancellationToken);

        return new PagedResult<WarehouseZoneDto>(
            items.Select(MapToDto).ToList(),
            totalCount,
            query.Page,
            query.PageSize);
    }

    public async Task<WarehouseZoneDto?> GetByIdAsync(
        int id,
        CancellationToken cancellationToken)
    {
        var zone = await _repository.GetByIdAsync(
            id, cancellationToken);

        return zone is null ? null : MapToDto(zone);
    }

    public async Task<WarehouseZoneDto> CreateAsync(
        CreateWarehouseZoneRequest request,
        CancellationToken cancellationToken)
    {
        Validate(request.Code, request.Name);

        if (!await _repository.WarehouseIsActiveAsync(
            request.WarehouseId, cancellationToken))
        {
            throw new InvalidWarehouseZoneException(
                "Magazyn nie istnieje lub jest nieaktywny.");
        }

        var code = request.Code.Trim().ToUpperInvariant();

        if (await _repository.CodeExistsAsync(
            request.WarehouseId,
            code,
            null,
            cancellationToken))
        {
            throw new DuplicateWarehouseZoneCodeException();
        }

        var zone = new WarehouseZone
        {
            WarehouseId = request.WarehouseId,
            Code = code,
            Name = request.Name.Trim(),
            IsActive = true
        };

        await _repository.AddAsync(
            zone, cancellationToken);

        await _repository.SaveChangesAsync(
            cancellationToken);

        // Ponowny odczyt pobiera rowniez kod magazynu.
        var created = await _repository.GetByIdAsync(
            zone.Id, cancellationToken);

        return created is null
            ? throw new InvalidOperationException(
                "Created warehouse zone could not be loaded.")
            : MapToDto(created);
    }

    public async Task<WarehouseZoneDto?> UpdateAsync(
        int id,
        UpdateWarehouseZoneRequest request,
        CancellationToken cancellationToken)
    {
        var zone = await _repository.GetTrackedByIdAsync(
            id, cancellationToken);

        if (zone is null)
            return null;

        Validate(request.Code, request.Name);

        if (request.IsActive && !zone.Warehouse.IsActive)
        {
            throw new InvalidWarehouseZoneException(
                "Nie mozna aktywowac strefy w nieaktywnym magazynie.");
        }

        var code = request.Code.Trim().ToUpperInvariant();

        if (await _repository.CodeExistsAsync(
            zone.WarehouseId,
            code,
            id,
            cancellationToken))
        {
            throw new DuplicateWarehouseZoneCodeException();
        }

        if (zone.IsActive && !request.IsActive)
        {
            await EnsureCanDeactivateAsync(
                id, cancellationToken);
        }

        zone.Code = code;
        zone.Name = request.Name.Trim();
        zone.IsActive = request.IsActive;

        await _repository.SaveChangesAsync(
            cancellationToken);

        return MapToDto(zone);
    }

    public async Task<bool> DeactivateAsync(
        int id,
        CancellationToken cancellationToken)
    {
        var zone = await _repository.GetTrackedByIdAsync(
            id, cancellationToken);

        if (zone is null)
            return false;

        if (zone.IsActive)
        {
            await EnsureCanDeactivateAsync(
                id, cancellationToken);

            zone.IsActive = false;

            await _repository.SaveChangesAsync(
                cancellationToken);
        }

        return true;
    }

    private async Task EnsureCanDeactivateAsync(
        int id,
        CancellationToken cancellationToken)
    {
        if (await _repository.HasLocatedLogisticUnitsAsync(
            id, cancellationToken))
        {
            throw new InvalidWarehouseZoneException(
                "Nie mozna dezaktywowac strefy z przypisanymi paletami.");
        }
    }

    private static void Validate(
        string code,
        string name)
    {
        if (string.IsNullOrWhiteSpace(code) ||
            string.IsNullOrWhiteSpace(name))
        {
            throw new InvalidWarehouseZoneException(
                "Kod i nazwa strefy nie moga byc puste.");
        }
    }

    private static WarehouseZoneDto MapToDto(
        WarehouseZone zone)
    {
        return new WarehouseZoneDto(
            zone.Id,
            zone.WarehouseId,
            zone.Warehouse.Code,
            zone.Code,
            zone.Name,
            zone.IsActive);
    }
}
