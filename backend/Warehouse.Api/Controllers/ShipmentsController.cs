
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Warehouse.Application.DTOs.Shipments;
using Warehouse.Application.Interfaces.Services;
using Warehouse.Infrastructure.Identity;

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

    [Authorize(
        Roles = WarehouseRoles.Administrator + "," +
                WarehouseRoles.Manager)]
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

    [Authorize(
        Roles = WarehouseRoles.Administrator + "," +
                WarehouseRoles.Manager)]
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
