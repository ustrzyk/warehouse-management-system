
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Warehouse.Application.DTOs.Common;
using Warehouse.Application.DTOs.Warehouses;
using Warehouse.Application.Interfaces.Services;
using Warehouse.Infrastructure.Identity;

namespace Warehouse.Api.Controllers;

[ApiController]
[Route("api/warehouses")]
public class WarehousesController : ControllerBase
{
    private readonly IWarehouseService _service;

    public WarehousesController(
        IWarehouseService service)
    {
        _service = service;
    }

    [HttpGet]
    public async Task<ActionResult<PagedResult<WarehouseDto>>> GetPage(
        [FromQuery] WarehouseListQuery query,
        CancellationToken cancellationToken)
    {
        var result = await _service.GetPageAsync(
            query, cancellationToken);

        return Ok(result);
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<WarehouseDto>> GetById(
        int id,
        CancellationToken cancellationToken)
    {
        if (id <= 0)
            return BadRequest("Invalid warehouse ID.");

        var result = await _service.GetByIdAsync(
            id, cancellationToken);

        return result is null
            ? NotFound()
            : Ok(result);
    }

    [Authorize(Roles = WarehouseRoles.StructureEditors)]
    [HttpPost]
    public async Task<ActionResult<WarehouseDto>> Create(
        [FromBody] CreateWarehouseRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _service.CreateAsync(
            request, cancellationToken);

        return CreatedAtAction(
            nameof(GetById),
            new { id = result.Id },
            result);
    }

    [Authorize(Roles = WarehouseRoles.StructureEditors)]
    [HttpPut("{id:int}")]
    public async Task<ActionResult<WarehouseDto>> Update(
        int id,
        [FromBody] UpdateWarehouseRequest request,
        CancellationToken cancellationToken)
    {
        if (id <= 0)
            return BadRequest("Invalid warehouse ID.");

        var result = await _service.UpdateAsync(
            id, request, cancellationToken);

        return result is null
            ? NotFound()
            : Ok(result);
    }

    [Authorize(Roles = WarehouseRoles.StructureEditors)]
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(
        int id,
        CancellationToken cancellationToken)
    {
        if (id <= 0)
            return BadRequest("Invalid warehouse ID.");

        var deactivated = await _service.DeactivateAsync(
            id, cancellationToken);

        return deactivated
            ? NoContent()
            : NotFound();
    }
}
