using System;
using System.Collections.Generic;
using System.Text;


namespace Warehouse.Application.DTOs.WarehouseZones;

public record WarehouseZoneDto(
    int Id,
    int WarehouseId,
    string WarehouseCode,
    string Code,
    string Name,
    bool IsActive
);
