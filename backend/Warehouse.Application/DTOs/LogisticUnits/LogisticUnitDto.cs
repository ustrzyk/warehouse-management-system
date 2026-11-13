using System;
using System.Collections.Generic;
using System.Text;


namespace Warehouse.Application.DTOs.LogisticUnits;

public record LogisticUnitDto(
    int Id,
    string Code,
    string Status,
    int? StorageLocationId,
    string? StorageLocationCode,
    DateTime CreatedAtUtc
);
