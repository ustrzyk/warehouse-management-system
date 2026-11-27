
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Warehouse.Application.DTOs.Common;
using Warehouse.Application.DTOs.Stock;
using Warehouse.Application.Interfaces.Services;

namespace Warehouse.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/stock/availability")]
public class StockAvailabilityController : ControllerBase
{
    private readonly IStockAvailabilityService _service;

    public StockAvailabilityController(
        IStockAvailabilityService service)
    {
        _service = service;
    }

    [HttpGet]
    public async Task<ActionResult<
        PagedResult<StockAvailabilityDto>>> GetPage(
        [FromQuery] StockAvailabilityQuery query,
        CancellationToken cancellationToken)
    {
        var result = await _service.GetPageAsync(
            query, cancellationToken);

        return Ok(result);
    }
}
