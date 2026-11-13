
using Microsoft.AspNetCore.Mvc;
using Warehouse.Application.DTOs.Common;
using Warehouse.Application.DTOs.LogisticUnits;
using Warehouse.Application.Interfaces.Services;

namespace Warehouse.Api.Controllers;

[ApiController]
[Route("api/logistic-units")]
public class LogisticUnitsController : ControllerBase
{
    private readonly ILogisticUnitService _service;

    public LogisticUnitsController(
        ILogisticUnitService service)
    {
        _service = service;
    }

    [HttpGet]
    public async Task<ActionResult<PagedResult<LogisticUnitDto>>> GetPage(
        [FromQuery] LogisticUnitListQuery query,
        CancellationToken cancellationToken)
    {
        var result = await _service.GetPageAsync(
            query, cancellationToken);

        return Ok(result);
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<LogisticUnitDto>> GetById(
        int id,
        CancellationToken cancellationToken)
    {
        if (id <= 0)
            return BadRequest("Invalid logistic unit ID.");

        var unit = await _service.GetByIdAsync(
            id, cancellationToken);

        return unit is null ? NotFound() : Ok(unit);
    }

    [HttpPost]
    public async Task<ActionResult<LogisticUnitDto>> Create(
        [FromBody] CreateLogisticUnitRequest request,
        CancellationToken cancellationToken)
    {
        var unit = await _service.CreateAsync(
            request, cancellationToken);

        return CreatedAtAction(
            nameof(GetById),
            new { id = unit.Id },
            unit);
    }


    [HttpPost("{id:int}/store")]
    public async Task<ActionResult<LogisticUnitDto>> Store(
        int id,
        [FromBody] StoreLogisticUnitRequest request,
        CancellationToken cancellationToken)
    {
        if (id <= 0)
            return BadRequest("Invalid logistic unit ID.");

        var unit = await _service.StoreAsync(
            id, request, cancellationToken);

        return unit is null ? NotFound() : Ok(unit);
    }

}
