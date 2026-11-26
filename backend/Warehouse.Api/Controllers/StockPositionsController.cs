
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Warehouse.Application.DTOs.Common;
using Warehouse.Application.DTOs.Stock;
using Warehouse.Application.Interfaces.Services;

namespace Warehouse.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/stock/positions")]
public class StockPositionsController : ControllerBase
{
    private readonly IStockPositionsService _service;

    public StockPositionsController(
        IStockPositionsService service)
    {
        _service = service;
    }

    [HttpGet]
    public async Task<ActionResult<
        PagedResult<StockPositionDto>>> GetPage(
        [FromQuery] StockPositionQuery query,
        CancellationToken cancellationToken)
    {
        var result = await _service.GetPageAsync(
            query, cancellationToken);

        return Ok(result);
    }
}
