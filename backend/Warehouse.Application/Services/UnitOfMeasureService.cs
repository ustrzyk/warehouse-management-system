using Warehouse.Application.DTOs.Common;
using Warehouse.Application.DTOs.UnitsOfMeasure;
using Warehouse.Application.Exceptions;
using Warehouse.Application.Interfaces.Repositories;
using Warehouse.Application.Interfaces.Services;
using Warehouse.Domain.Entities;

namespace Warehouse.Application.Services;

public class UnitOfMeasureService : IUnitOfMeasureService
{
    private readonly IUnitOfMeasureRepository _repository;

    public UnitOfMeasureService(
        IUnitOfMeasureRepository repository)
    {
        _repository = repository;
    }

    public async Task<PagedResult<UnitOfMeasureDto>> GetPageAsync(
        UnitOfMeasureListQuery query,
        CancellationToken cancellationToken)
    {
        var (items, totalCount) =
            await _repository.GetPageAsync(
                query, cancellationToken);

        return new PagedResult<UnitOfMeasureDto>(
            items.Select(MapToDto).ToList(),
            totalCount,
            query.Page,
            query.PageSize);
    }

    public async Task<UnitOfMeasureDto?> GetByIdAsync(
        int id,
        CancellationToken cancellationToken)
    {
        var unit = await _repository.GetByIdAsync(
            id, cancellationToken);

        return unit is null ? null : MapToDto(unit);
    }

    public async Task<UnitOfMeasureDto> CreateAsync(
        CreateUnitOfMeasureRequest request,
        CancellationToken cancellationToken)
    {
        var code = request.Code.Trim().ToUpperInvariant();

        if (await _repository.CodeExistsAsync(
            code, null, cancellationToken))
        {
            throw new DuplicateUnitOfMeasureCodeException();
        }

        var unit = new UnitOfMeasure
        {
            Code = code,
            Name = request.Name.Trim(),
            Symbol = request.Symbol.Trim(),
            IsActive = true
        };

        await _repository.AddAsync(unit, cancellationToken);
        await _repository.SaveChangesAsync(cancellationToken);

        return MapToDto(unit);
    }

    public async Task<UnitOfMeasureDto?> UpdateAsync(
        int id,
        UpdateUnitOfMeasureRequest request,
        CancellationToken cancellationToken)
    {
        var unit = await _repository.GetTrackedByIdAsync(
            id, cancellationToken);

        if (unit is null)
            return null;

        var code = request.Code.Trim().ToUpperInvariant();

        if (await _repository.CodeExistsAsync(
            code, id, cancellationToken))
        {
            throw new DuplicateUnitOfMeasureCodeException();
        }

        unit.Code = code;
        unit.Name = request.Name.Trim();
        unit.Symbol = request.Symbol.Trim();
        unit.IsActive = request.IsActive;

        await _repository.SaveChangesAsync(cancellationToken);

        return MapToDto(unit);
    }

    public async Task<bool> DeactivateAsync(
        int id,
        CancellationToken cancellationToken)
    {
        var unit = await _repository.GetTrackedByIdAsync(
            id, cancellationToken);

        if (unit is null)
            return false;

        if (unit.IsActive)
        {
            unit.IsActive = false;
            await _repository.SaveChangesAsync(cancellationToken);
        }

        return true;
    }

    private static UnitOfMeasureDto MapToDto(
        UnitOfMeasure unit)
    {
        return new UnitOfMeasureDto(
            unit.Id,
            unit.Code,
            unit.Name,
            unit.Symbol,
            unit.IsActive);
    }
}