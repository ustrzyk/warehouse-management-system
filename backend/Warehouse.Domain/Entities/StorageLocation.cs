using System;
using System.Collections.Generic;
using System.Text;


namespace Warehouse.Domain.Entities;

public class StorageLocation
{
    public int Id { get; set; }

    public int WarehouseZoneId { get; set; }

    public WarehouseZone WarehouseZone { get; set; } = null!;

    public string Code { get; set; } = string.Empty;

    public string? Description { get; set; }

    public bool IsActive { get; set; } = true;
}
