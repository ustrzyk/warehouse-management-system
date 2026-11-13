
using System.ComponentModel.DataAnnotations;

namespace Warehouse.Application.DTOs.LogisticUnitItems;

public class CreateLogisticUnitItemRequest
{
    [Range(1, int.MaxValue)]
    public int ProductId { get; set; }

    [Range(1, int.MaxValue)]
    public int? ProductBatchId { get; set; }

    [Range(
        typeof(decimal),
        "0.001",
        "999999999999999.999",
        ParseLimitsInInvariantCulture = true,
        ConvertValueInInvariantCulture = true)]
    public decimal Quantity { get; set; }
}
