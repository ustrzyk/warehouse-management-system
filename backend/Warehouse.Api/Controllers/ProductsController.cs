
using Microsoft.AspNetCore.Mvc;
using Warehouse.Application.DTOs.Common;
using Warehouse.Application.DTOs.Products;
using Warehouse.Application.Exceptions;
using Warehouse.Application.Interfaces.Services;

namespace Warehouse.Api.Controllers;

[ApiController]
[Route("api/products")]
public class ProductsController : ControllerBase
{
    private readonly IProductService _service;

    public ProductsController(IProductService service)
    {
        _service = service;
    }

    [HttpGet]
    public async Task<ActionResult<PagedResult<ProductDto>>> GetPage(
        [FromQuery] ProductListQuery query,
        CancellationToken cancellationToken)
    {
        var result = await _service.GetPageAsync(
            query, cancellationToken);

        return Ok(result);
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<ProductDto>> GetById(
        int id,
        CancellationToken cancellationToken)
    {
        if (id <= 0)
            return BadRequest("Invalid product ID.");

        var product = await _service.GetByIdAsync(
            id, cancellationToken);

        return product is null ? NotFound() : Ok(product);
    }

    [HttpPost]
    public async Task<ActionResult<ProductDto>> Create(
        [FromBody] CreateProductRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var product = await _service.CreateAsync(
                request, cancellationToken);

            return CreatedAtAction(
                nameof(GetById),
                new { id = product.Id },
                product);
        }
        catch (DuplicateProductCodeException ex)
        {
            return Conflict(new ProblemDetails
            {
                Title = "Duplicate product code",
                Detail = ex.Message,
                Status = StatusCodes.Status409Conflict
            });
        }
        catch (InvalidProductReferenceException ex)
        {
            return BadRequest(new ProblemDetails
            {
                Title = "Invalid product reference",
                Detail = ex.Message,
                Status = StatusCodes.Status400BadRequest
            });
        }
    }

    [HttpPut("{id:int}")]
    public async Task<ActionResult<ProductDto>> Update(
        int id,
        [FromBody] UpdateProductRequest request,
        CancellationToken cancellationToken)
    {
        if (id <= 0)
            return BadRequest("Invalid product ID.");

        try
        {
            var product = await _service.UpdateAsync(
                id, request, cancellationToken);

            return product is null ? NotFound() : Ok(product);
        }
        catch (DuplicateProductCodeException ex)
        {
            return Conflict(new ProblemDetails
            {
                Title = "Duplicate product code",
                Detail = ex.Message,
                Status = StatusCodes.Status409Conflict
            });
        }
        catch (InvalidProductReferenceException ex)
        {
            return BadRequest(new ProblemDetails
            {
                Title = "Invalid product reference",
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
            return BadRequest("Invalid product ID.");

        var deactivated = await _service.DeactivateAsync(
            id, cancellationToken);

        return deactivated ? NoContent() : NotFound();
    }
}
