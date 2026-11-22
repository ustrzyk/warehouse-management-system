
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Warehouse.Application.DTOs.Common;
using Warehouse.Application.DTOs.LogisticUnits;
using Warehouse.Application.DTOs.LogisticUnitMovements;
using Warehouse.Application.Interfaces.Services;
using Warehouse.Infrastructure.Identity;

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

        return unit is null
            ? NotFound()
            : Ok(unit);
    }

    [Authorize(Roles = WarehouseRoles.LogisticsOperators)]
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

    [Authorize(Roles = WarehouseRoles.LogisticsOperators)]
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

        return unit is null
            ? NotFound()
            : Ok(unit);
    }

    [Authorize(Roles = WarehouseRoles.LogisticsOperators)]
    [HttpPost("{id:int}/move")]
    public async Task<ActionResult<LogisticUnitMovementDto>> Move(
        int id,
        [FromBody] MoveLogisticUnitRequest request,
        CancellationToken cancellationToken)
    {
        if (id <= 0)
            return BadRequest("Invalid logistic unit ID.");

        var movement = await _service.MoveAsync(
            id, request, cancellationToken);

        if (movement is null)
            return NotFound();

        return CreatedAtAction(
            nameof(GetMovementById),
            new
            {
                id,
                movementId = movement.Id
            },
            movement);
    }

    [HttpGet("{id:int}/movements")]
    public async Task<ActionResult<
        PagedResult<LogisticUnitMovementDto>>> GetMovements(
        int id,
        [FromQuery] LogisticUnitMovementListQuery query,
        CancellationToken cancellationToken)
    {
        if (id <= 0)
            return BadRequest("Invalid logistic unit ID.");

        var result = await _service.GetMovementsPageAsync(
            id, query, cancellationToken);

        return result is null
            ? NotFound()
            : Ok(result);
    }

    [HttpGet("{id:int}/movements/{movementId:int}")]
    public async Task<ActionResult<LogisticUnitMovementDto>>
        GetMovementById(
            int id,
            int movementId,
            CancellationToken cancellationToken)
    {
        if (id <= 0 || movementId <= 0)
            return BadRequest("Invalid ID.");

        var result = await _service.GetMovementByIdAsync(
            id, movementId, cancellationToken);

        return result is null
            ? NotFound()
            : Ok(result);
    }
}
