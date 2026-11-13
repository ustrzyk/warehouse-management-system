
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
