
using Microsoft.AspNetCore.Mvc;
using Warehouse.Application.DTOs.Common;
using Warehouse.Application.DTOs.ProductBatches;
using Warehouse.Application.Exceptions;
using Warehouse.Application.Interfaces.Services;

namespace Warehouse.Api.Controllers;

[ApiController]
[Route("api/product-batches")]
public class ProductBatchesController : ControllerBase
{
    private readonly IProductBatchService _service;

    public ProductBatchesController(
        IProductBatchService service)
    {
        _service = service;
    }

    [HttpGet]
    public async Task<ActionResult<PagedResult<ProductBatchDto>>>
        GetPage(
            [FromQuery] ProductBatchListQuery query,
            CancellationToken cancellationToken)
    {
        var result = await _service.GetPageAsync(
            query, cancellationToken);

        return Ok(result);
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<ProductBatchDto>> GetById(
        int id,
        CancellationToken cancellationToken)
    {
        if (id <= 0)
            return BadRequest("Invalid batch ID.");

        var batch = await _service.GetByIdAsync(
            id, cancellationToken);

        return batch is null ? NotFound() : Ok(batch);
    }

    [HttpPost]
    public async Task<ActionResult<ProductBatchDto>> Create(
        [FromBody] CreateProductBatchRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var batch = await _service.CreateAsync(
                request, cancellationToken);

            return CreatedAtAction(
                nameof(GetById),
                new { id = batch.Id },
                batch);
        }
        catch (DuplicateProductBatchException ex)
        {
            return Conflict(new ProblemDetails
            {
                Title = "Duplicate product batch",
                Detail = ex.Message,
                Status = StatusCodes.Status409Conflict
            });
        }
        catch (InvalidProductBatchException ex)
        {
            return BadRequest(new ProblemDetails
            {
                Title = "Invalid product batch",
                Detail = ex.Message,
                Status = StatusCodes.Status400BadRequest
            });
        }
    }

    [HttpPut("{id:int}")]
    public async Task<ActionResult<ProductBatchDto>> Update(
        int id,
        [FromBody] UpdateProductBatchRequest request,
        CancellationToken cancellationToken)
    {
        if (id <= 0)
            return BadRequest("Invalid batch ID.");

        try
        {
            var batch = await _service.UpdateAsync(
                id, request, cancellationToken);

            return batch is null ? NotFound() : Ok(batch);
        }
        catch (DuplicateProductBatchException ex)
        {
            return Conflict(new ProblemDetails
            {
                Title = "Duplicate product batch",
                Detail = ex.Message,
                Status = StatusCodes.Status409Conflict
            });
        }
        catch (InvalidProductBatchException ex)
        {
            return BadRequest(new ProblemDetails
            {
                Title = "Invalid product batch",
                Detail = ex.Message,
                Status = StatusCodes.Status400BadRequest
            });
        }
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(
        int id,
        CancellationToken cancellationToken)
    {
        if (id <= 0)
            return BadRequest("Invalid batch ID.");

        var deactivated = await _service.DeactivateAsync(
            id, cancellationToken);

        return deactivated ? NoContent() : NotFound();
    }
}
