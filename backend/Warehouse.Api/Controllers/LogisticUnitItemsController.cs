
using Microsoft.AspNetCore.Mvc;
using Warehouse.Application.DTOs.LogisticUnitItems;
using Warehouse.Application.Interfaces.Services;

namespace Warehouse.Api.Controllers;

[ApiController]
[Route("api/logistic-units/{logisticUnitId:int}/items")]
public class LogisticUnitItemsController : ControllerBase
{
    private readonly ILogisticUnitItemService _service;

    public LogisticUnitItemsController(
        ILogisticUnitItemService service)
    {
        _service = service;
    }

    [HttpGet]
    public async Task<ActionResult<
        IReadOnlyList<LogisticUnitItemDto>>> GetItems(
        int logisticUnitId,
        CancellationToken cancellationToken)
    {
        if (logisticUnitId <= 0)
            return BadRequest("Invalid logistic unit ID.");

        var items = await _service.GetItemsAsync(
            logisticUnitId, cancellationToken);

        return items is null ? NotFound() : Ok(items);
    }

    [HttpGet("{itemId:int}")]
    public async Task<ActionResult<LogisticUnitItemDto>> GetById(
        int logisticUnitId,
        int itemId,
        CancellationToken cancellationToken)
    {
        if (logisticUnitId <= 0 || itemId <= 0)
            return BadRequest("Invalid ID.");

        var item = await _service.GetByIdAsync(
            logisticUnitId, itemId, cancellationToken);

        return item is null ? NotFound() : Ok(item);
    }

    [HttpPost]
    public async Task<ActionResult<LogisticUnitItemDto>> Create(
        int logisticUnitId,
        [FromBody] CreateLogisticUnitItemRequest request,
        CancellationToken cancellationToken)
    {
        if (logisticUnitId <= 0)
            return BadRequest("Invalid logistic unit ID.");

        var item = await _service.CreateAsync(
            logisticUnitId, request, cancellationToken);

        if (item is null)
            return NotFound();

        return CreatedAtAction(
            nameof(GetById),
            new
            {
                logisticUnitId,
                itemId = item.Id
            },
            item);
    }
}
