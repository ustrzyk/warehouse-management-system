using Microsoft.AspNetCore.Mvc;
using Warehouse.Application.DTOs.Common;
using Warehouse.Application.DTOs.UnitsOfMeasure;
using Warehouse.Application.Exceptions;
using Warehouse.Application.Interfaces.Services;

namespace Warehouse.Api.Controllers;

[ApiController]
[Route("api/units-of-measure")]
public class UnitsOfMeasureController : ControllerBase
{
    private readonly IUnitOfMeasureService _service;

    public UnitsOfMeasureController(IUnitOfMeasureService service)
    {
        _service = service;
    }

    [HttpGet]
    public async Task<ActionResult<PagedResult<UnitOfMeasureDto>>>
        GetPage(
            [FromQuery] UnitOfMeasureListQuery query,
            CancellationToken cancellationToken)
    {
        var result = await _service.GetPageAsync(
            query, cancellationToken);

        return Ok(result);
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<UnitOfMeasureDto>> GetById(
        int id,
        CancellationToken cancellationToken)
    {
        if (id <= 0)
            return BadRequest("Invalid unit ID.");

        var unit = await _service.GetByIdAsync(
            id, cancellationToken);

        return unit is null ? NotFound() : Ok(unit);
    }

    [HttpPost]
    public async Task<ActionResult<UnitOfMeasureDto>> Create(
        [FromBody] CreateUnitOfMeasureRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var unit = await _service.CreateAsync(
                request, cancellationToken);

            return CreatedAtAction(
                nameof(GetById),
                new { id = unit.Id },
                unit);
        }
        catch (DuplicateUnitOfMeasureCodeException ex)
        {
            return Conflict(new ProblemDetails
            {
                Title = "Duplicate unit code",
                Detail = ex.Message,
                Status = StatusCodes.Status409Conflict
            });
        }
    }

    [HttpPut("{id:int}")]
    public async Task<ActionResult<UnitOfMeasureDto>> Update(
        int id,
        [FromBody] UpdateUnitOfMeasureRequest request,
        CancellationToken cancellationToken)
    {
        if (id <= 0)
            return BadRequest("Invalid unit ID.");

        try
        {
            var unit = await _service.UpdateAsync(
                id, request, cancellationToken);

            return unit is null ? NotFound() : Ok(unit);
        }
        catch (DuplicateUnitOfMeasureCodeException ex)
        {
            return Conflict(new ProblemDetails
            {
                Title = "Duplicate unit code",
                Detail = ex.Message,
                Status = StatusCodes.Status409Conflict
            });
        }
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(
        int id,
        CancellationToken cancellationToken)
    {
        if (id <= 0)
            return BadRequest("Invalid unit ID.");

        var deactivated = await _service.DeactivateAsync(
            id, cancellationToken);

        return deactivated ? NoContent() : NotFound();
    }
}