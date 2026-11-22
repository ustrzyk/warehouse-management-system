
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Warehouse.Application.DTOs.Common;
using Warehouse.Application.DTOs.WarehouseZones;
using Warehouse.Application.Interfaces.Services;
using Warehouse.Infrastructure.Identity;

namespace Warehouse.Api.Controllers;

[ApiController]
[Route("api/warehouse-zones")]
public class WarehouseZonesController : ControllerBase
{
    private readonly IWarehouseZoneService _service;

    public WarehouseZonesController(
        IWarehouseZoneService service)
    {
        _service = service;
    }

    [HttpGet]
    public async Task<ActionResult<PagedResult<WarehouseZoneDto>>>
        GetPage(
            [FromQuery] WarehouseZoneListQuery query,
            CancellationToken cancellationToken)
    {
        var result = await _service.GetPageAsync(
            query, cancellationToken);

        return Ok(result);
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<WarehouseZoneDto>> GetById(
        int id,
        CancellationToken cancellationToken)
    {
        if (id <= 0)
            return BadRequest("Invalid zone ID.");

        var zone = await _service.GetByIdAsync(
            id, cancellationToken);

        return zone is null
            ? NotFound()
            : Ok(zone);
    }

    [Authorize(Roles = WarehouseRoles.StructureEditors)]
    [HttpPost]
    public async Task<ActionResult<WarehouseZoneDto>> Create(
        [FromBody] CreateWarehouseZoneRequest request,
        CancellationToken cancellationToken)
    {
        var zone = await _service.CreateAsync(
            request, cancellationToken);

        return CreatedAtAction(
            nameof(GetById),
            new { id = zone.Id },
            zone);
    }

    [Authorize(Roles = WarehouseRoles.StructureEditors)]
    [HttpPut("{id:int}")]
    public async Task<ActionResult<WarehouseZoneDto>> Update(
        int id,
        [FromBody] UpdateWarehouseZoneRequest request,
        CancellationToken cancellationToken)
    {
        if (id <= 0)
            return BadRequest("Invalid zone ID.");

        var zone = await _service.UpdateAsync(
            id, request, cancellationToken);

        return zone is null
            ? NotFound()
            : Ok(zone);
    }

    [Authorize(Roles = WarehouseRoles.StructureEditors)]
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(
        int id,
        CancellationToken cancellationToken)
    {
        if (id <= 0)
            return BadRequest("Invalid zone ID.");

        var deactivated = await _service.DeactivateAsync(
            id, cancellationToken);

        return deactivated
            ? NoContent()
            : NotFound();
    }
}
