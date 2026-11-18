using System;
using System.Collections.Generic;
using System.Text;


namespace Warehouse.Application.DTOs.Picking;

public record PickingOperationDto(
    int Id,
    Guid RequestId,
    int WarehouseOrderId,
    int WarehouseOrderItemId,
    int LogisticUnitId,
    int LogisticUnitItemId,
    string LogisticUnitCode,
    string ProductCode,
    string? BatchNumber,
    string SourceLocationCode,
    decimal Quantity,
    DateTime PickedAtUtc
);
