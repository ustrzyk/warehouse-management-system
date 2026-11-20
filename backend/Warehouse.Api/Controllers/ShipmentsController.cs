
using Microsoft.AspNetCore.Mvc;
using Warehouse.Application.DTOs.Shipments;
using Warehouse.Application.Interfaces.Services;

namespace Warehouse.Api.Controllers;

[ApiController]
[Route("api/shipments")]
public class ShipmentsController : ControllerBase
{
    private readonly IShipmentService _service;

    public ShipmentsController(IShipmentService service)
    {
        _service = service;
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<ShipmentDto>> GetById(
        int id,
        CancellationToken cancellationToken)
    {
        if (id <= 0)
            return BadRequest("Invalid shipment ID.");

        var shipment = await _service.GetByIdAsync(
            id, cancellationToken);

        return shipment is null
            ? NotFound()
            : Ok(shipment);
    }

    [HttpGet("by-order/{orderId:int}")]
    public async Task<ActionResult<ShipmentDto>> GetByOrderId(
        int orderId,
        CancellationToken cancellationToken)
    {
        if (orderId <= 0)
            return BadRequest("Invalid warehouse order ID.");

        var shipment = await _service.GetByOrderIdAsync(
            orderId, cancellationToken);

        return shipment is null
            ? NotFound()
            : Ok(shipment);
    }

    [HttpPost]
    public async Task<ActionResult<ShipmentDto>> Create(
        [FromBody] CreateShipmentRequest request,
        CancellationToken cancellationToken)
    {
        var shipment = await _service.CreateAsync(
            request, cancellationToken);

        return CreatedAtAction(
            nameof(GetById),
            new { id = shipment.Id },
            shipment);
    }

    [HttpPost("{id:int}/dispatch")]
    public async Task<ActionResult<ShipmentDto>> Dispatch(
        int id,
        CancellationToken cancellationToken)
    {
        if (id <= 0)
            return BadRequest("Invalid shipment ID.");

        var shipment = await _service.DispatchAsync(
            id, cancellationToken);

        return shipment is null
            ? NotFound()
            : Ok(shipment);
    }
}
