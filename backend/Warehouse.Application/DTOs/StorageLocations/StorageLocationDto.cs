using System;
using System.Collections.Generic;
using System.Text;


namespace Warehouse.Application.DTOs.StorageLocations;

public record StorageLocationDto(
    int Id,
    int WarehouseZoneId,
    string WarehouseZoneCode,
    int WarehouseId,
    string WarehouseCode,
    string Code,
    string? Description,
    bool IsActive
);
