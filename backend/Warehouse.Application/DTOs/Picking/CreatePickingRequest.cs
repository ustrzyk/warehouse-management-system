
using System.ComponentModel.DataAnnotations;

namespace Warehouse.Application.DTOs.Picking;

public class CreatePickingRequest
{
    [Required]
    public Guid RequestId { get; set; }

    [Range(1, int.MaxValue)]
    public int WarehouseOrderItemId { get; set; }

    [Range(1, int.MaxValue)]
    public int LogisticUnitItemId { get; set; }

    [Range(
        typeof(decimal),
        "0.001",
        "999999999999999.999",
        ParseLimitsInInvariantCulture = true,
        ConvertValueInInvariantCulture = true)]
    public decimal Quantity { get; set; }
}
