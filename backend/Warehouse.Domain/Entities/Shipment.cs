
using Warehouse.Domain.Enums;

namespace Warehouse.Domain.Entities;

public class Shipment
{
    public int Id { get; set; }

    public string Number { get; set; } = string.Empty;

    public int WarehouseOrderId { get; set; }
    public WarehouseOrder WarehouseOrder { get; set; } = null!;

    public string CarrierName { get; set; } = string.Empty;

    public string? TrackingNumber { get; set; }

    public ShipmentStatus Status { get; set; }
        = ShipmentStatus.Prepared;

    public DateTime CreatedAtUtc { get; set; }
        = DateTime.UtcNow;

    public DateTime? ShippedAtUtc { get; set; }

    public int Revision { get; set; }

    public byte[] RowVersion { get; set; } = Array.Empty<byte>();
}
