
using System.ComponentModel.DataAnnotations;

namespace Warehouse.Application.DTOs.GoodsReceipts;

public class CreateGoodsReceiptRequest
{
    [Required]
    [StringLength(50, MinimumLength = 1)]
    public string Number { get; set; } = string.Empty;

    [Range(1, int.MaxValue)]
    public int WarehouseId { get; set; }

    [StringLength(150)]
    public string? SupplierName { get; set; }
}
