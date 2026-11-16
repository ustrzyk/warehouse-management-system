
using Microsoft.AspNetCore.Mvc;
using Warehouse.Application.DTOs.Common;
using Warehouse.Application.DTOs.GoodsReceipts;
using Warehouse.Application.Interfaces.Services;

namespace Warehouse.Api.Controllers;

[ApiController]
[Route("api/goods-receipts")]
public class GoodsReceiptsController : ControllerBase
{
    private readonly IGoodsReceiptService _service;

    public GoodsReceiptsController(
        IGoodsReceiptService service)
    {
        _service = service;
    }

    [HttpGet]
    public async Task<ActionResult<PagedResult<GoodsReceiptDto>>>
        GetPage(
            [FromQuery] GoodsReceiptListQuery query,
            CancellationToken cancellationToken)
    {
        var result = await _service.GetPageAsync(
            query, cancellationToken);

        return Ok(result);
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<GoodsReceiptDto>> GetById(
        int id,
        CancellationToken cancellationToken)
    {
        if (id <= 0)
            return BadRequest("Invalid goods receipt ID.");

        var receipt = await _service.GetByIdAsync(
            id, cancellationToken);

        return receipt is null
            ? NotFound()
            : Ok(receipt);
    }

    [HttpPost]
    public async Task<ActionResult<GoodsReceiptDto>> Create(
        [FromBody] CreateGoodsReceiptRequest request,
        CancellationToken cancellationToken)
    {
        var receipt = await _service.CreateAsync(
            request, cancellationToken);

        return CreatedAtAction(
            nameof(GetById),
            new { id = receipt.Id },
            receipt);
    }

    [HttpGet("{id:int}/items")]
    public async Task<ActionResult<
        IReadOnlyList<GoodsReceiptItemDto>>> GetItems(
        int id,
        CancellationToken cancellationToken)
    {
        if (id <= 0)
            return BadRequest("Invalid goods receipt ID.");

        var items = await _service.GetItemsAsync(
            id, cancellationToken);

        return items is null
            ? NotFound()
            : Ok(items);
    }

    [HttpPost("{id:int}/items")]
    public async Task<ActionResult<GoodsReceiptItemDto>> AddItem(
        int id,
        [FromBody] AddGoodsReceiptItemRequest request,
        CancellationToken cancellationToken)
    {
        if (id <= 0)
            return BadRequest("Invalid goods receipt ID.");

        var item = await _service.AddItemAsync(
            id, request, cancellationToken);

        return item is null
            ? NotFound()
            : StatusCode(StatusCodes.Status201Created, item);
    }
}
