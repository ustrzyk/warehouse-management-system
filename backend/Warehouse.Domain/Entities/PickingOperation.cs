using System;
using System.Collections.Generic;
using System.Text;


namespace Warehouse.Domain.Entities;

public class PickingOperation
{
    public int Id { get; set; }

    // Identyfikator zadania wysylany przez klienta.
    // Chroni przed ponownym wykonaniem tego samego pobrania.
    public Guid RequestId { get; set; }

    public int WarehouseOrderId { get; set; }
    public WarehouseOrder WarehouseOrder { get; set; } = null!;

    public int WarehouseOrderItemId { get; set; }
    public WarehouseOrderItem WarehouseOrderItem { get; set; } = null!;

    public int LogisticUnitId { get; set; }
    public LogisticUnit LogisticUnit { get; set; } = null!;

    public int LogisticUnitItemId { get; set; }
    public LogisticUnitItem LogisticUnitItem { get; set; } = null!;

    // Dane historyczne z momentu kompletacji.
    public string LogisticUnitCode { get; set; } = string.Empty;
    public string ProductCode { get; set; } = string.Empty;
    public string? BatchNumber { get; set; }
    public string SourceLocationCode { get; set; } = string.Empty;

    public decimal Quantity { get; set; }

    public DateTime PickedAtUtc { get; set; } = DateTime.UtcNow;
}
