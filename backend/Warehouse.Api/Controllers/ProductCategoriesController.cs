
using Microsoft.AspNetCore.Mvc;
using Warehouse.Application.DTOs.Common;
using Warehouse.Application.DTOs.ProductCategories;
using Warehouse.Application.Exceptions;
using Warehouse.Application.Interfaces.Services;

namespace Warehouse.Api.Controllers;

[ApiController]
[Route("api/product-categories")]
public class ProductCategoriesController : ControllerBase
{
    private readonly IProductCategoryService _service;

    public ProductCategoriesController(
        IProductCategoryService service)
    {
        _service = service;
    }

    [HttpGet]
    public async Task<ActionResult<PagedResult<ProductCategoryDto>>>
        GetPage(
            [FromQuery] ProductCategoryListQuery query,
            CancellationToken cancellationToken)
    {
        var result = await _service.GetPageAsync(
            query,
            cancellationToken);

        return Ok(result);
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<ProductCategoryDto>> GetById(
        int id,
        CancellationToken cancellationToken)
    {
        if (id <= 0)
            return BadRequest("Invalid category ID.");

        var category = await _service.GetByIdAsync(
            id,
            cancellationToken);

        return category is null
            ? NotFound()
            : Ok(category);
    }

    [HttpPost]
    public async Task<ActionResult<ProductCategoryDto>> Create(
        [FromBody] CreateProductCategoryRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var category = await _service.CreateAsync(
                request,
                cancellationToken);

            return CreatedAtAction(
                nameof(GetById),
                new { id = category.Id },
                category);
        }
        catch (DuplicateProductCategoryCodeException ex)
        {
            return Conflict(new ProblemDetails
            {
                Title = "Duplicate category code",
                Detail = ex.Message,
                Status = StatusCodes.Status409Conflict
            });
        }
    }

    [HttpPut("{id:int}")]
    public async Task<ActionResult<ProductCategoryDto>> Update(
        int id,
        [FromBody] UpdateProductCategoryRequest request,
        CancellationToken cancellationToken)
    {
        if (id <= 0)
            return BadRequest("Invalid category ID.");

        try
        {
            var category = await _service.UpdateAsync(
                id,
                request,
                cancellationToken);

            return category is null
                ? NotFound()
                : Ok(category);
        }
        catch (DuplicateProductCategoryCodeException ex)
        {
            return Conflict(new ProblemDetails
            {
                Title = "Duplicate category code",
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
            return BadRequest("Invalid category ID.");

        var deactivated = await _service.DeactivateAsync(
            id,
            cancellationToken);

        if (!deactivated)
            return NotFound();

        return NoContent();
    }
}
