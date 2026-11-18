
using Microsoft.AspNetCore.Mvc;
using Warehouse.Application.DTOs.Common;
using Warehouse.Application.DTOs.WarehouseOrders;
using Warehouse.Application.Interfaces.Services;

namespace Warehouse.Api.Controllers;

[ApiController]
[Route("api/warehouse-orders")]
public class WarehouseOrdersController : ControllerBase
{
    private readonly IWarehouseOrderService _service;

    public WarehouseOrdersController(
        IWarehouseOrderService service)
    {
        _service = service;
    }

    [HttpGet]
    public async Task<ActionResult<PagedResult<WarehouseOrderDto>>>
        GetPage(
            [FromQuery] WarehouseOrderListQuery query,
            CancellationToken cancellationToken)
    {
        var result = await _service.GetPageAsync(
            query, cancellationToken);

        return Ok(result);
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<WarehouseOrderDto>> GetById(
        int id,
        CancellationToken cancellationToken)
    {
        if (id <= 0)
            return BadRequest("Invalid warehouse order ID.");

        var order = await _service.GetByIdAsync(
            id, cancellationToken);

        return order is null
            ? NotFound()
            : Ok(order);
    }

    [HttpPost]
    public async Task<ActionResult<WarehouseOrderDto>> Create(
        [FromBody] CreateWarehouseOrderRequest request,
        CancellationToken cancellationToken)
    {
        var order = await _service.CreateAsync(
            request, cancellationToken);

        return CreatedAtAction(
            nameof(GetById),
            new { id = order.Id },
            order);
    }

    [HttpGet("{id:int}/items")]
    public async Task<ActionResult<
        IReadOnlyList<WarehouseOrderItemDto>>> GetItems(
        int id,
        CancellationToken cancellationToken)
    {
        if (id <= 0)
            return BadRequest("Invalid warehouse order ID.");

        var items = await _service.GetItemsAsync(
            id, cancellationToken);

        return items is null
            ? NotFound()
            : Ok(items);
    }

    [HttpPost("{id:int}/items")]
    public async Task<ActionResult<WarehouseOrderItemDto>> AddItem(
        int id,
        [FromBody] AddWarehouseOrderItemRequest request,
        CancellationToken cancellationToken)
    {
        if (id <= 0)
            return BadRequest("Invalid warehouse order ID.");

        var item = await _service.AddItemAsync(
            id, request, cancellationToken);

        return item is null
            ? NotFound()
            : StatusCode(StatusCodes.Status201Created, item);
    }

    [HttpPost("{id:int}/release")]
    public async Task<ActionResult<WarehouseOrderDto>> Release(
        int id,
        CancellationToken cancellationToken)
    {
        if (id <= 0)
            return BadRequest("Invalid warehouse order ID.");

        var order = await _service.ReleaseAsync(
            id, cancellationToken);

        return order is null
            ? NotFound()
            : Ok(order);
    }

}
