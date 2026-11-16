using System;
using System.Collections.Generic;
using System.Text;


namespace Warehouse.Domain.Entities;

public class LogisticUnitMovement
{
    public int Id { get; set; }

    public int LogisticUnitId { get; set; }
    public LogisticUnit LogisticUnit { get; set; } = null!;

    public int FromStorageLocationId { get; set; }
    public StorageLocation FromLocation { get; set; } = null!;

    public int ToStorageLocationId { get; set; }
    public StorageLocation ToLocation { get; set; } = null!;

    // Kody lokalizacji w momencie ruchu.
    public string FromLocationCode { get; set; } = string.Empty;
    public string ToLocationCode { get; set; } = string.Empty;

    public DateTime MovedAtUtc { get; set; } = DateTime.UtcNow;

    public string? Note { get; set; }
}
