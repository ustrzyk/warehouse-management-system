using System;
using System.Collections.Generic;
using System.Text;


namespace Warehouse.Application.DTOs.WarehouseOrders;

public record WarehouseOrderDto(
    int Id,
    string Number,
    int WarehouseId,
    string WarehouseCode,
    string? CustomerName,
    string Status,
    DateTime CreatedAtUtc,
    int Revision,
    DateTime? ReleasedAtUtc,
    DateTime? CompletedAtUtc
);
