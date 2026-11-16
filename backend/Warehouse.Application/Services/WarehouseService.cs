
using Warehouse.Application.DTOs.Common;
using Warehouse.Application.DTOs.Warehouses;
using Warehouse.Application.Exceptions;
using Warehouse.Application.Interfaces.Repositories;
using Warehouse.Application.Interfaces.Services;
using WarehouseEntity = global::Warehouse.Domain.Entities.Warehouse;

namespace Warehouse.Application.Services;

public class WarehouseService : IWarehouseService
{
    private readonly IWarehouseRepository _repository;

    public WarehouseService(IWarehouseRepository repository)
    {
        _repository = repository;
    }

    public async Task<PagedResult<WarehouseDto>> GetPageAsync(
        WarehouseListQuery query,
        CancellationToken cancellationToken)
    {
        var (items, totalCount) =
            await _repository.GetPageAsync(
                query, cancellationToken);

        return new PagedResult<WarehouseDto>(
            items.Select(MapToDto).ToList(),
            totalCount,
            query.Page,
            query.PageSize);
    }

    public async Task<WarehouseDto?> GetByIdAsync(
        int id,
        CancellationToken cancellationToken)
    {
        var warehouse = await _repository.GetByIdAsync(
            id, cancellationToken);

        return warehouse is null ? null : MapToDto(warehouse);
    }

    public async Task<WarehouseDto> CreateAsync(
        CreateWarehouseRequest request,
        CancellationToken cancellationToken)
    {
        Validate(request.Code, request.Name);

        var code = request.Code.Trim().ToUpperInvariant();

        if (await _repository.CodeExistsAsync(
            code, null, cancellationToken))
        {
            throw new DuplicateWarehouseCodeException();
        }

        var warehouse = new WarehouseEntity
        {
            Code = code,
            Name = request.Name.Trim(),
            IsActive = true
        };

        await _repository.AddAsync(
            warehouse, cancellationToken);

        await _repository.SaveChangesAsync(cancellationToken);

        return MapToDto(warehouse);
    }

    public async Task<WarehouseDto?> UpdateAsync(
        int id,
        UpdateWarehouseRequest request,
        CancellationToken cancellationToken)
    {
        var warehouse = await _repository.GetTrackedByIdAsync(
            id, cancellationToken);

        if (warehouse is null)
            return null;

        Validate(request.Code, request.Name);

        var code = request.Code.Trim().ToUpperInvariant();

        if (await _repository.CodeExistsAsync(
            code, id, cancellationToken))
        {
            throw new DuplicateWarehouseCodeException();
        }

        if (warehouse.IsActive && !request.IsActive)
        {
            await EnsureCanDeactivateAsync(
                id, cancellationToken);
        }

        warehouse.Code = code;
        warehouse.Name = request.Name.Trim();
        warehouse.IsActive = request.IsActive;

        await _repository.SaveChangesAsync(cancellationToken);

        return MapToDto(warehouse);
    }

    public async Task<bool> DeactivateAsync(
        int id,
        CancellationToken cancellationToken)
    {
        var warehouse = await _repository.GetTrackedByIdAsync(
            id, cancellationToken);

        if (warehouse is null)
            return false;

        if (warehouse.IsActive)
        {
            await EnsureCanDeactivateAsync(
                id, cancellationToken);

            warehouse.IsActive = false;
            await _repository.SaveChangesAsync(cancellationToken);
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
            throw new InvalidWarehouseException(
                "Nie mozna dezaktywowac magazynu " +
                "z przypisanymi paletami.");
        }
    }

    private static void Validate(string code, string name)
    {
        if (string.IsNullOrWhiteSpace(code) ||
            string.IsNullOrWhiteSpace(name))
        {
            throw new InvalidWarehouseException(
                "Kod i nazwa magazynu nie moga byc puste.");
        }
    }

    private static WarehouseDto MapToDto(
        WarehouseEntity warehouse)
    {
        return new WarehouseDto(
            warehouse.Id,
            warehouse.Code,
            warehouse.Name,
            warehouse.IsActive);
    }
}
