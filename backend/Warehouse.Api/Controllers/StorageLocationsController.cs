
using Microsoft.AspNetCore.Mvc;
using Warehouse.Application.DTOs.Common;
using Warehouse.Application.DTOs.StorageLocations;
using Warehouse.Application.Interfaces.Services;

namespace Warehouse.Api.Controllers;

[ApiController]
[Route("api/storage-locations")]
public class StorageLocationsController : ControllerBase
{
    private readonly IStorageLocationService _service;

    public StorageLocationsController(
        IStorageLocationService service)
    {
        _service = service;
    }

    [HttpGet]
    public async Task<ActionResult<PagedResult<StorageLocationDto>>>
        GetPage(
            [FromQuery] StorageLocationListQuery query,
            CancellationToken cancellationToken)
    {
        var result = await _service.GetPageAsync(
            query, cancellationToken);

        return Ok(result);
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<StorageLocationDto>> GetById(
        int id,
        CancellationToken cancellationToken)
    {
        if (id <= 0)
            return BadRequest("Invalid storage location ID.");

        var location = await _service.GetByIdAsync(
            id, cancellationToken);

        return location is null
            ? NotFound()
            : Ok(location);
    }

    [HttpPost]
    public async Task<ActionResult<StorageLocationDto>> Create(
        [FromBody] CreateStorageLocationRequest request,
        CancellationToken cancellationToken)
    {
        var location = await _service.CreateAsync(
            request, cancellationToken);

        return CreatedAtAction(
            nameof(GetById),
            new { id = location.Id },
            location);
    }

    [HttpPut("{id:int}")]
    public async Task<ActionResult<StorageLocationDto>> Update(
        int id,
        [FromBody] UpdateStorageLocationRequest request,
        CancellationToken cancellationToken)
    {
        if (id <= 0)
            return BadRequest("Invalid storage location ID.");

        var location = await _service.UpdateAsync(
            id, request, cancellationToken);

        return location is null
            ? NotFound()
            : Ok(location);
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(
        int id,
        CancellationToken cancellationToken)
    {
        if (id <= 0)
            return BadRequest("Invalid storage location ID.");

        var deactivated = await _service.DeactivateAsync(
            id, cancellationToken);

        return deactivated
            ? NoContent()
            : NotFound();
    }
}
