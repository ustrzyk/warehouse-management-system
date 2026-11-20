
using System.ComponentModel.DataAnnotations;

namespace Warehouse.Application.DTOs.Shipments;

public class CreateShipmentRequest
{
    [Range(1, int.MaxValue)]
    public int WarehouseOrderId { get; set; }

    [Required]
    [StringLength(100)]
    public string CarrierName { get; set; } = string.Empty;

    [StringLength(100)]
    public string? TrackingNumber { get; set; }
}
