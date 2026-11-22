
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Warehouse.Application.DTOs.Common;
using Warehouse.Application.DTOs.Picking;
using Warehouse.Application.Interfaces.Services;
using Warehouse.Infrastructure.Identity;

namespace Warehouse.Api.Controllers;

[ApiController]
[Route("api/warehouse-orders/{orderId:int}/picks")]
public class PickingOperationsController : ControllerBase
{
    private readonly IPickingService _service;

    public PickingOperationsController(IPickingService service)
    {
        _service = service;
    }

    [Authorize(
        Roles = WarehouseRoles.Administrator + "," +
                WarehouseRoles.WarehouseWorker)]
    [HttpPost]
    public async Task<ActionResult<PickingOperationDto>> Create(
        int orderId,
        [FromBody] CreatePickingRequest request,
        CancellationToken cancellationToken)
    {
        if (orderId <= 0)
            return BadRequest("Invalid warehouse order ID.");

        var operation = await _service.CreateAsync(
            orderId, request, cancellationToken);

        // Also returns HTTP 200 for an idempotent retry.
        return operation is null
            ? NotFound()
            : Ok(operation);
    }

    [HttpGet]
    public async Task<ActionResult<
        PagedResult<PickingOperationDto>>> GetPage(
        int orderId,
        [FromQuery] PickingListQuery query,
        CancellationToken cancellationToken)
    {
        if (orderId <= 0)
            return BadRequest("Invalid warehouse order ID.");

        var result = await _service.GetPageAsync(
            orderId, query, cancellationToken);

        return result is null
            ? NotFound()
            : Ok(result);
    }

    [HttpGet("{pickingId:int}")]
    public async Task<ActionResult<PickingOperationDto>> GetById(
        int orderId,
        int pickingId,
        CancellationToken cancellationToken)
    {
        if (orderId <= 0 || pickingId <= 0)
            return BadRequest("Invalid ID.");

        var result = await _service.GetByIdAsync(
            orderId, pickingId, cancellationToken);

        return result is null
            ? NotFound()
            : Ok(result);
    }
}
