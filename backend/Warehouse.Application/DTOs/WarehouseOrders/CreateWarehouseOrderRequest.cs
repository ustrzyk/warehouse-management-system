
using System.ComponentModel.DataAnnotations;

namespace Warehouse.Application.DTOs.WarehouseOrders;

public class CreateWarehouseOrderRequest
{
    [Required]
    [StringLength(50, MinimumLength = 1)]
    public string Number { get; set; } = string.Empty;

    [Range(1, int.MaxValue)]
    public int WarehouseId { get; set; }

    [StringLength(150)]
    public string? CustomerName { get; set; }
}
