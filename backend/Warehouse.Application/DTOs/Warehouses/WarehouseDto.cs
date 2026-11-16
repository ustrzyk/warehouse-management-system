using System;
using System.Collections.Generic;
using System.Text;


namespace Warehouse.Application.DTOs.Warehouses;

public record WarehouseDto(
    int Id,
    string Code,
    string Name,
    bool IsActive
);
